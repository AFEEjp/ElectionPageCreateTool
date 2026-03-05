using System.Data.SQLite;
using System.Reflection;
using ClosedXML.Excel;
using ElectionPageCreateTool.Attributes;
using Microsoft.Extensions.Configuration;

namespace ElectionPageCreateTool.Logic
{
    public class DatabaseOperationLogic
    {
        private readonly string _dbFilePath;

        /// <summary>
        /// コンストラクタ - appsettings.jsonから設定を読み込む
        /// </summary>
        public DatabaseOperationLogic()
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            _dbFilePath = configuration["DatabaseSettings:DbFilePath"]
                ?? throw new InvalidOperationException("DatabaseSettings:DbFilePath is not configured in appsettings.json");
        }

        /// <summary>
        /// 接続文字列を取得
        /// </summary>
        private string GetConnectionString()
        {
            return $"Data Source={_dbFilePath};Version=3;";
        }

        /// <summary>
        /// モデルクラスからカラムマッピング情報を取得
        /// </summary>
        private static ColumnMappingInfo GetColumnMappingInfo<T>()
        {
            var properties = typeof(T).GetProperties()
                .Where(p => p.GetCustomAttribute<ExcelColumnAttribute>() != null)
                .ToList();

            var mappings = new Dictionary<string, string>();
            var upsertKeys = new List<(string excelHeader, string columnName, bool isFurigana)>();

            foreach (var prop in properties)
            {
                var attr = prop.GetCustomAttribute<ExcelColumnAttribute>()!;
                mappings[attr.HeaderName] = prop.Name;

                if (attr.IsUpsertKey)
                {
                    upsertKeys.Add((attr.HeaderName, prop.Name, attr.IsFurigana));
                }
            }

            return new ColumnMappingInfo(mappings, upsertKeys);
        }

        /// <summary>
        /// ExcelデータをSQLiteに追加または更新（Upsert）- モデルクラス指定版（テーブル名指定）
        /// </summary>
        /// <typeparam name="T">モデルクラス</typeparam>
        /// <param name="tableName">テーブル名</param>
        /// <param name="data">Excelから変換されたDictionaryデータ</param>
        public void UpsertDataToSQLite<T>(string tableName, List<Dictionary<string, string>> data) where T : class
        {
            if (string.IsNullOrWhiteSpace(tableName))
            {
                throw new ArgumentException("テーブル名を指定してください。", nameof(tableName));
            }

            if (data == null || data.Count == 0)
            {
                throw new ArgumentException("挿入するデータがありません。", nameof(data));
            }

            var mappingInfo = GetColumnMappingInfo<T>();

            if (mappingInfo.UpsertKeys.Count == 0)
            {
                throw new InvalidOperationException($"モデルクラス {typeof(T).Name} にUpsertKeyが設定されていません。ExcelColumnAttributeのIsUpsertKey=trueを設定してください。");
            }

            // データの最初の行からキーを取得してカラムを決定
            var allExcelKeys = data[0].Keys.ToList();

            // 除外するカラム名（マッピング後の名前）
            var excludeColumns = new HashSet<string> { "Num" ,"Id"};

            // カラムマッピングを適用し、除外対象を除いたペアを作成
            var filteredPairs = allExcelKeys
                .Select(key => (
                    ExcelKey: key,
                    ColumnName: mappingInfo.ColumnMappings.TryGetValue(key, out var mappedName) ? mappedName : key
                ))
                .Where(pair => !excludeColumns.Contains(pair.ColumnName))
                .ToList();

            var excelKeys = filteredPairs.Select(p => p.ExcelKey).ToList();
            var columnDefinitions = filteredPairs.Select(p => p.ColumnName).ToList();

            // テーブル作成用カラム（Excelにないカラムも含める）
            var additionalColumns = new List<string> { "Result" };
            var tableColumns = columnDefinitions.Concat(additionalColumns).ToList();

            using var connection = new SQLiteConnection(GetConnectionString());
            connection.Open();

            using var transaction = connection.BeginTransaction();

            try
            {
                // テーブル作成（存在しない場合）
                CreateTable(connection, tableName, tableColumns);

                // データをUpsert
                var (insertedCount, updatedCount, skippedCount) = UpsertData(connection, tableName, data, excelKeys, columnDefinitions, mappingInfo);

                transaction.Commit();
                Console.WriteLine($"テーブル '{tableName}' に {insertedCount} 件を追加、{updatedCount} 件を更新、{skippedCount} 件をスキップしました。");
            }
            catch (Exception)
            {
                transaction.Rollback();
                throw;
            }
        }

        /// <summary>
        /// テーブルを作成（created_at, updated_at付き）+ 履歴テーブルとトリガー
        /// </summary>
        private void CreateTable(SQLiteConnection connection, string tableName, List<string> columnNames)
        {
            // テーブルの存在チェック
            var tableExistsSql = $"SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=@tableName";
            using (var checkCommand = connection.CreateCommand())
            {
                checkCommand.CommandText = tableExistsSql;
                checkCommand.Parameters.Add(new SQLiteParameter("@tableName", tableName));
                var exists = Convert.ToInt32(checkCommand.ExecuteScalar()) > 0;

                if (exists)
                {
                    return;
                }
            }

            // メインテーブル作成
            var columns = string.Join(", ", columnNames.Select(c => $"[{c}] TEXT"));
            var createTableSql = $"CREATE TABLE [{tableName}] (id INTEGER PRIMARY KEY AUTOINCREMENT, {columns}, created_at TEXT, updated_at TEXT)";

            using (var command = connection.CreateCommand())
            {
                command.CommandText = createTableSql;
                command.ExecuteNonQuery();
            }

            Console.WriteLine($"テーブル '{tableName}' を作成しました。カラム: {string.Join(", ", columnNames)}, created_at, updated_at");

            // 履歴テーブルとトリガーを作成
            CreateHistoryTableAndTriggers(connection, tableName, columnNames);
        }

        /// <summary>
        /// 履歴テーブルとトリガーを作成
        /// </summary>
        private void CreateHistoryTableAndTriggers(SQLiteConnection connection, string tableName, List<string> columnNames)
        {
            var historyTableName = $"{tableName}_History";

            // 履歴テーブル作成（メインテーブルのカラム + 履歴用メタデータ）
            var columns = string.Join(", ", columnNames.Select(c => $"[{c}] TEXT"));
            var createHistorySql = $@"
                CREATE TABLE [{historyTableName}] (
                    _HistoryId INTEGER PRIMARY KEY AUTOINCREMENT,
                    _Operation TEXT NOT NULL,
                    _ValidFrom TEXT NOT NULL,
                    _ValidTo TEXT,
                    id INTEGER,
                    {columns},
                    created_at TEXT,
                    updated_at TEXT
                )";

            using (var command = connection.CreateCommand())
            {
                command.CommandText = createHistorySql;
                command.ExecuteNonQuery();
            }

            // カラム名リスト（トリガー用）
            var allColumns = new[] { "id" }.Concat(columnNames).Concat(new[] { "created_at", "updated_at" }).ToList();
            var columnList = string.Join(", ", allColumns.Select(c => $"[{c}]"));
            var oldColumnList = string.Join(", ", allColumns.Select(c => $"OLD.[{c}]"));
            var newColumnList = string.Join(", ", allColumns.Select(c => $"NEW.[{c}]"));

            // INSERT トリガー
            var insertTriggerSql = $@"
                CREATE TRIGGER [{tableName}_Insert_History]
                AFTER INSERT ON [{tableName}]
                BEGIN
                    INSERT INTO [{historyTableName}] (_Operation, _ValidFrom, {columnList})
                    VALUES ('INSERT', datetime('now', 'localtime'), {newColumnList});
                END";

            using (var command = connection.CreateCommand())
            {
                command.CommandText = insertTriggerSql;
                command.ExecuteNonQuery();
            }

            // UPDATE トリガー（更新前のレコードを履歴に保存）
            var updateTriggerSql = $@"
                CREATE TRIGGER [{tableName}_Update_History]
                AFTER UPDATE ON [{tableName}]
                BEGIN
                    UPDATE [{historyTableName}]
                    SET _ValidTo = datetime('now', 'localtime')
                    WHERE id = OLD.id AND _ValidTo IS NULL;

                    INSERT INTO [{historyTableName}] (_Operation, _ValidFrom, {columnList})
                    VALUES ('UPDATE', datetime('now', 'localtime'), {newColumnList});
                END";

            using (var command = connection.CreateCommand())
            {
                command.CommandText = updateTriggerSql;
                command.ExecuteNonQuery();
            }

            // DELETE トリガー
            var deleteTriggerSql = $@"
                CREATE TRIGGER [{tableName}_Delete_History]
                AFTER DELETE ON [{tableName}]
                BEGIN
                    UPDATE [{historyTableName}]
                    SET _ValidTo = datetime('now', 'localtime')
                    WHERE id = OLD.id AND _ValidTo IS NULL;

                    INSERT INTO [{historyTableName}] (_Operation, _ValidFrom, _ValidTo, {columnList})
                    VALUES ('DELETE', datetime('now', 'localtime'), datetime('now', 'localtime'), {oldColumnList});
                END";

            using (var command = connection.CreateCommand())
            {
                command.CommandText = deleteTriggerSql;
                command.ExecuteNonQuery();
            }

            Console.WriteLine($"履歴テーブル '{historyTableName}' とトリガーを作成しました。");
        }

        /// <summary>
        /// データをUpsert（追加または更新）- 新ロジック
        /// Step1: ふりがなのみで検索
        /// Step2: 0件→INSERT、1件or複数件→選挙区+政党で絞り込み
        /// Step3: 一致なし→ユーザー確認
        /// </summary>
        private (int insertedCount, int updatedCount, int skippedCount) UpsertData(SQLiteConnection connection, string tableName,
            List<Dictionary<string, string>> data, List<string> excelKeys, List<string> columnNames, ColumnMappingInfo mappingInfo)
        {
            var insertedCount = 0;
            var updatedCount = 0;
            var skippedCount = 0;
            var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            // ふりがなキーを取得
            var furiganaKey = mappingInfo.UpsertKeys.FirstOrDefault(k => k.isFurigana);
            if (furiganaKey == default)
            {
                throw new InvalidOperationException("ふりがなキー（IsFurigana=true）が設定されていません。");
            }

            // ふりがな以外のUpsertキー（選挙区、政党など）
            var otherKeys = mappingInfo.UpsertKeys.Where(k => !k.isFurigana).ToList();

            foreach (var row in data)
            {
                var furiganaValue = row.TryGetValue(furiganaKey.excelHeader, out var fv) ? fv : "";

                // Step1: ふりがなのみで既存レコードを検索
                var furiganaMatches = FindRecordsByFurigana(connection, tableName, furiganaValue, furiganaKey.columnName, columnNames);

                if (furiganaMatches.Count == 0)
                {
                    // 0件ヒット → 新規INSERT
                    InsertRecord(connection, tableName, row, excelKeys, columnNames, now);
                    insertedCount++;
                    Console.WriteLine($"  [新規] {furiganaValue}");
                }
                else
                {
                    // 1件以上ヒット → 選挙区+政党で絞り込み
                    var exactMatches = FindExactMatches(furiganaMatches, row, otherKeys);

                    if (exactMatches.Count == 1)
                    {
                        // 選挙区+政党も一致する1件 → 値比較して更新orスキップ
                        var match = exactMatches[0];
                        var changes = GetChanges(row, match.data, excelKeys, columnNames);
                        if (changes.Count > 0)
                        {
                            UpdateRecord(connection, tableName, match.id, row, excelKeys, columnNames, now);
                            updatedCount++;
                            OutputChanges($"更新: {furiganaValue}", changes);
                        }
                        else
                        {
                            skippedCount++;
                        }
                    }
                    else if (exactMatches.Count > 1)
                    {
                        // 選挙区+政党一致が複数件（通常ありえないが念のため）
                        Console.WriteLine($"  [警告] 完全一致レコードが複数見つかりました: {furiganaValue}");
                        var match = exactMatches[0]; // 最初の1件を更新
                        var changes = GetChanges(row, match.data, excelKeys, columnNames);
                        if (changes.Count > 0)
                        {
                            UpdateRecord(connection, tableName, match.id, row, excelKeys, columnNames, now);
                            updatedCount++;
                            OutputChanges($"更新: {furiganaValue}", changes);
                        }
                        else
                        {
                            skippedCount++;
                        }
                    }
                    else
                    {
                        // 選挙区+政党一致が0件 → ユーザー確認
                        var action = PromptUserForAction(row, furiganaMatches, furiganaKey, otherKeys);

                        switch (action.Action)
                        {
                            case UserAction.Update:
                                // 選択されたレコードのデータを取得して変更内容を表示
                                var selectedRecord = furiganaMatches.FirstOrDefault(r => r.id == action.TargetId!.Value);
                                UpdateRecord(connection, tableName, action.TargetId!.Value, row, excelKeys, columnNames, now);
                                updatedCount++;
                                if (selectedRecord != default)
                                {
                                    var manualChanges = GetChanges(row, selectedRecord.data, excelKeys, columnNames);
                                    OutputChanges($"更新(手動): {furiganaValue}", manualChanges);
                                }
                                else
                                {
                                    Console.WriteLine($"  [更新(手動)] {furiganaValue}");
                                }
                                break;
                            case UserAction.Insert:
                                InsertRecord(connection, tableName, row, excelKeys, columnNames, now);
                                insertedCount++;
                                Console.WriteLine($"  [新規(手動)] {furiganaValue}");
                                break;
                            case UserAction.Skip:
                                skippedCount++;
                                Console.WriteLine($"  [スキップ(手動)] {furiganaValue}");
                                break;
                        }
                    }
                }
            }

            // 処理完了後、重複候補を出力
            OutputDuplicateCandidates(connection, tableName, furiganaKey.columnName, columnNames, otherKeys);

            return (insertedCount, updatedCount, skippedCount);
        }

        /// <summary>
        /// ユーザーアクションの種類
        /// </summary>
        private enum UserAction
        {
            Update,
            Insert,
            Skip
        }

        /// <summary>
        /// ユーザーアクションの結果
        /// </summary>
        private class UserActionResult
        {
            public UserAction Action { get; set; }
            public long? TargetId { get; set; }
        }

        /// <summary>
        /// ふりがなのみでレコードを検索
        /// </summary>
        private List<(long id, Dictionary<string, string> data)> FindRecordsByFurigana(
            SQLiteConnection connection, string tableName, string furiganaValue, string furiganaColumn, List<string> columnNames)
        {
            var results = new List<(long id, Dictionary<string, string> data)>();
            var normalizedValue = NormalizeForComparison(furiganaValue);

            var selectColumns = string.Join(", ", new[] { "id" }.Concat(columnNames.Select(c => $"[{c}]")));
            var selectSql = $"SELECT {selectColumns} FROM [{tableName}] WHERE REPLACE(REPLACE([{furiganaColumn}], ' ', ''), '　', '') = @furigana";

            using var command = connection.CreateCommand();
            command.CommandText = selectSql;
            command.Parameters.Add(new SQLiteParameter("@furigana", normalizedValue));

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var id = reader.GetInt64(0);
                var data = new Dictionary<string, string>();

                for (int i = 0; i < columnNames.Count; i++)
                {
                    var value = reader.IsDBNull(i + 1) ? "" : reader.GetString(i + 1);
                    data[columnNames[i]] = value;
                }

                results.Add((id, data));
            }

            return results;
        }

        /// <summary>
        /// ふりがな一致レコードから選挙区+政党も一致するものを抽出
        /// </summary>
        private List<(long id, Dictionary<string, string> data)> FindExactMatches(
            List<(long id, Dictionary<string, string> data)> furiganaMatches,
            Dictionary<string, string> row,
            List<(string excelHeader, string columnName, bool isFurigana)> otherKeys)
        {
            return furiganaMatches.Where(match =>
            {
                foreach (var key in otherKeys)
                {
                    var excelValue = row.TryGetValue(key.excelHeader, out var ev) ? ev : "";
                    var dbValue = match.data.TryGetValue(key.columnName, out var dv) ? dv : "";
                    if (excelValue != dbValue)
                    {
                        return false;
                    }
                }
                return true;
            }).ToList();
        }

        /// <summary>
        /// ユーザーに更新/新規/スキップを確認
        /// </summary>
        private UserActionResult PromptUserForAction(
            Dictionary<string, string> row,
            List<(long id, Dictionary<string, string> data)> existingRecords,
            (string excelHeader, string columnName, bool isFurigana) furiganaKey,
            List<(string excelHeader, string columnName, bool isFurigana)> otherKeys)
        {
            var furiganaValue = row.TryGetValue(furiganaKey.excelHeader, out var fv) ? fv : "";

            Console.WriteLine();
            Console.WriteLine($"========================================");
            Console.WriteLine($"[確認] ふりがな「{furiganaValue}」で {existingRecords.Count} 件の既存レコードが見つかりました。");
            Console.WriteLine($"----------------------------------------");
            Console.WriteLine($"【インポートデータ】");
            foreach (var key in otherKeys)
            {
                var value = row.TryGetValue(key.excelHeader, out var v) ? v : "";
                Console.WriteLine($"  {key.columnName}: {value}");
            }
            Console.WriteLine($"----------------------------------------");
            Console.WriteLine($"【既存レコード】");
            for (int i = 0; i < existingRecords.Count; i++)
            {
                var record = existingRecords[i];
                Console.WriteLine($"  [{i + 1}] ID:{record.id}");
                foreach (var key in otherKeys)
                {
                    var value = record.data.TryGetValue(key.columnName, out var v) ? v : "";
                    Console.WriteLine($"      {key.columnName}: {value}");
                }
            }
            Console.WriteLine($"----------------------------------------");
            Console.WriteLine($"選択してください:");
            for (int i = 0; i < existingRecords.Count; i++)
            {
                Console.WriteLine($"  {i + 1}: レコード[{i + 1}]を更新");
            }
            Console.WriteLine($"  N: 新規登録");
            Console.WriteLine($"  S: スキップ");
            Console.WriteLine($"========================================");

            while (true)
            {
                Console.Write("入力: ");
                var input = Console.ReadLine()?.Trim().ToUpper();

                if (input == "N")
                {
                    return new UserActionResult { Action = UserAction.Insert };
                }
                else if (input == "S")
                {
                    return new UserActionResult { Action = UserAction.Skip };
                }
                else if (int.TryParse(input, out var num) && num >= 1 && num <= existingRecords.Count)
                {
                    return new UserActionResult
                    {
                        Action = UserAction.Update,
                        TargetId = existingRecords[num - 1].id
                    };
                }
                else
                {
                    Console.WriteLine("無効な入力です。再度入力してください。");
                }
            }
        }

        /// <summary>
        /// 処理完了後、ふりがなが重複するレコードを出力
        /// </summary>
        private void OutputDuplicateCandidates(
            SQLiteConnection connection, string tableName, string furiganaColumn,
            List<string> columnNames, List<(string excelHeader, string columnName, bool isFurigana)> otherKeys)
        {
            // ふりがなでグループ化して2件以上あるものを取得
            var sql = $@"
                SELECT REPLACE(REPLACE([{furiganaColumn}], ' ', ''), '　', '') as normalized_furigana, COUNT(*) as cnt
                FROM [{tableName}]
                GROUP BY normalized_furigana
                HAVING cnt > 1
                ORDER BY cnt DESC";

            var duplicateFuriganas = new List<string>();

            using (var command = connection.CreateCommand())
            {
                command.CommandText = sql;
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    duplicateFuriganas.Add(reader.GetString(0));
                }
            }

            if (duplicateFuriganas.Count == 0)
            {
                return;
            }

            Console.WriteLine();
            Console.WriteLine("========================================");
            Console.WriteLine("【重複の可能性があるデータ】");
            Console.WriteLine("========================================");

            foreach (var normalizedFurigana in duplicateFuriganas)
            {
                var selectColumns = string.Join(", ", new[] { "id", $"[{furiganaColumn}]" }.Concat(otherKeys.Select(k => $"[{k.columnName}]")));
                var selectSql = $"SELECT {selectColumns} FROM [{tableName}] WHERE REPLACE(REPLACE([{furiganaColumn}], ' ', ''), '　', '') = @furigana";

                using var command = connection.CreateCommand();
                command.CommandText = selectSql;
                command.Parameters.Add(new SQLiteParameter("@furigana", normalizedFurigana));

                var records = new List<(long id, string furigana, Dictionary<string, string> data)>();

                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    var id = reader.GetInt64(0);
                    var furigana = reader.IsDBNull(1) ? "" : reader.GetString(1);
                    var data = new Dictionary<string, string>();
                    for (int i = 0; i < otherKeys.Count; i++)
                    {
                        var value = reader.IsDBNull(i + 2) ? "" : reader.GetString(i + 2);
                        data[otherKeys[i].columnName] = value;
                    }
                    records.Add((id, furigana, data));
                }

                if (records.Count > 0)
                {
                    Console.WriteLine($"[{records[0].furigana}] {records.Count}件");
                    foreach (var record in records)
                    {
                        var details = string.Join(" / ", otherKeys.Select(k => record.data.TryGetValue(k.columnName, out var v) ? v : ""));
                        Console.WriteLine($"  - ID:{record.id} {details}");
                    }
                    Console.WriteLine();
                }
            }
        }

        /// <summary>
        /// ExcelデータとDBデータを比較して変更内容を取得
        /// </summary>
        private List<(string columnName, string oldValue, string newValue)> GetChanges(
            Dictionary<string, string> excelRow, Dictionary<string, string> dbRow,
            List<string> excelKeys, List<string> columnNames)
        {
            var changes = new List<(string columnName, string oldValue, string newValue)>();

            for (var i = 0; i < excelKeys.Count; i++)
            {
                var excelKey = excelKeys[i];
                var dbColumn = columnNames[i];

                var excelValue = excelRow.GetValueOrDefault(excelKey, "");
                var dbValue = dbRow.GetValueOrDefault(dbColumn, "");

                if (excelValue != dbValue)
                {
                    changes.Add((dbColumn, dbValue, excelValue));
                }
            }

            return changes;
        }

        /// <summary>
        /// 変更内容をコンソールに出力
        /// </summary>
        private void OutputChanges(string label, List<(string columnName, string oldValue, string newValue)> changes)
        {
            Console.WriteLine($"  [{label}]");
            foreach (var (columnName, oldValue, newValue) in changes)
            {
                var oldDisplay = string.IsNullOrEmpty(oldValue) ? "(空)" : Truncate(oldValue, 30);
                var newDisplay = string.IsNullOrEmpty(newValue) ? "(空)" : Truncate(newValue, 30);
                Console.WriteLine($"    {columnName}: 「{oldDisplay}」→「{newDisplay}」");
            }
        }

        /// <summary>
        /// 文字列を指定長で切り詰め
        /// </summary>
        private string Truncate(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value)) return value;
            // 改行を除去
            var singleLine = value.Replace("\r", "").Replace("\n", " ");
            if (singleLine.Length <= maxLength) return singleLine;
            return singleLine.Substring(0, maxLength) + "...";
        }

        /// <summary>
        /// 比較用に文字列を正規化（スペース除去）
        /// </summary>
        private string NormalizeForComparison(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }
            return value.Replace(" ", "").Replace("　", "");
        }

        /// <summary>
        /// レコードを更新
        /// </summary>
        private void UpdateRecord(SQLiteConnection connection, string tableName, long id,
            Dictionary<string, string> row, List<string> excelKeys, List<string> columnNames, string now)
        {
            var setClauses = columnNames.Select((col, i) => $"[{col}] = @p{i}").ToList();
            setClauses.Add("updated_at = @updated_at");

            var updateSql = $"UPDATE [{tableName}] SET {string.Join(", ", setClauses)} WHERE id = @id";

            using var command = connection.CreateCommand();
            command.CommandText = updateSql;

            for (int i = 0; i < excelKeys.Count; i++)
            {
                var value = row.TryGetValue(excelKeys[i], out var v) ? v : null;
                command.Parameters.Add(new SQLiteParameter($"@p{i}", value ?? (object)DBNull.Value));
            }
            command.Parameters.Add(new SQLiteParameter("@updated_at", now));
            command.Parameters.Add(new SQLiteParameter("@id", id));

            command.ExecuteNonQuery();
        }

        /// <summary>
        /// レコードを挿入
        /// </summary>
        private void InsertRecord(SQLiteConnection connection, string tableName,
            Dictionary<string, string> row, List<string> excelKeys, List<string> columnNames, string now)
        {
            var allColumns = columnNames.Concat(new[] { "created_at", "updated_at" }).ToList();
            var parameterNames = allColumns.Select((_, i) => $"@p{i}").ToList();
            var insertSql = $"INSERT INTO [{tableName}] ([{string.Join("], [", allColumns)}]) VALUES ({string.Join(", ", parameterNames)})";

            using var command = connection.CreateCommand();
            command.CommandText = insertSql;

            for (int i = 0; i < excelKeys.Count; i++)
            {
                var value = row.TryGetValue(excelKeys[i], out var v) ? v : null;
                command.Parameters.Add(new SQLiteParameter($"@p{i}", value ?? (object)DBNull.Value));
            }
            command.Parameters.Add(new SQLiteParameter($"@p{excelKeys.Count}", now));
            command.Parameters.Add(new SQLiteParameter($"@p{excelKeys.Count + 1}", now));

            command.ExecuteNonQuery();
        }

        /// <summary>
        /// SQLiteからデータを取得してモデルクラスのリストとして返す
        /// </summary>
        /// <typeparam name="T">モデルクラス</typeparam>
        /// <param name="tableName">テーブル名</param>
        /// <returns>モデルクラスのリスト</returns>
        public List<T> GetAllData<T>(string tableName) where T : class, new()
        {
            if (string.IsNullOrWhiteSpace(tableName))
            {
                throw new ArgumentException("テーブル名を指定してください。", nameof(tableName));
            }

            var result = new List<T>();
            var properties = typeof(T).GetProperties()
                .Where(p => p.GetCustomAttribute<ExcelColumnAttribute>() != null)
                .ToList();

            using var connection = new SQLiteConnection(GetConnectionString());
            connection.Open();

            // カラム名のリストを取得（プロパティ名がカラム名）
            var columnNames = properties.Select(p => p.Name).ToList();
            var selectColumns = string.Join(", ", columnNames.Select(c => $"[{c}]"));
            var selectSql = $"SELECT {selectColumns} FROM [{tableName}]";

            using var command = connection.CreateCommand();
            command.CommandText = selectSql;

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var item = new T();
                for (int i = 0; i < properties.Count; i++)
                {
                    var value = reader.IsDBNull(i) ? string.Empty : reader.GetString(i);
                    properties[i].SetValue(item, value);
                }
                result.Add(item);
            }

            Console.WriteLine($"テーブル '{tableName}' から {result.Count} 件のデータを取得しました。");
            return result;
        }

        /// <summary>
        /// モデルクラスのカラムマッピング設定を表示
        /// </summary>
        public void ShowColumnMappings<T>() where T : class
        {
            var mappingInfo = GetColumnMappingInfo<T>();
            Console.WriteLine($"カラムマッピング設定 ({typeof(T).Name}):");
            foreach (var mapping in mappingInfo.ColumnMappings)
            {
                Console.WriteLine($"  {mapping.Key} -> {mapping.Value}");
            }
        }

        /// <summary>
        /// モデルクラスのUpsertキーカラム設定を表示
        /// </summary>
        public void ShowUpsertKeyColumns<T>() where T : class
        {
            var mappingInfo = GetColumnMappingInfo<T>();
            Console.WriteLine($"更新判定に使用するキーカラム ({typeof(T).Name}):");
            foreach (var (excelHeader, columnName, isFurigana) in mappingInfo.UpsertKeys)
            {
                var suffix = isFurigana ? " [ふりがな]" : "";
                Console.WriteLine($"  {excelHeader} -> {columnName}{suffix}");
            }
        }

        /// <summary>
        /// データベースファイルのパスを取得
        /// </summary>
        public string GetDbFilePath()
        {
            return _dbFilePath;
        }

        /// <summary>
        /// SQLiteのデータをExcelファイルにエクスポート
        /// </summary>
        /// <typeparam name="T">モデルクラス</typeparam>
        /// <param name="tableName">テーブル名</param>
        /// <param name="outputFilePath">出力ファイルパス</param>
        /// <param name="excludeColumns">除外するカラム名の配列（デフォルトでcreated_at, updated_atは除外）</param>
        /// <param name="valueTransformer">値変換ロジック (item, propertyName, originalValue) => transformedValue</param>
        /// <param name="workbookCustomizer">保存前にワークブックをカスタマイズするコールバック</param>
        public void ExportToExcel<T>(
            string tableName,
            string outputFilePath,
            string[]? excludeColumns = null,
            Func<T, string, string, string>? valueTransformer = null,
            Action<IXLWorkbook, List<T>>? workbookCustomizer = null) where T : class, new()
        {
            if (string.IsNullOrWhiteSpace(tableName))
            {
                throw new ArgumentException("テーブル名を指定してください。", nameof(tableName));
            }

            // デフォルトの除外カラム
            var defaultExcludeColumns = new HashSet<string> { "created_at", "updated_at" };
            if (excludeColumns != null)
            {
                foreach (var col in excludeColumns)
                {
                    defaultExcludeColumns.Add(col);
                }
            }

            // モデルクラスからカラム情報を取得（プロパティ名 → 日本語ヘッダー名）
            var columnToHeaderMap = GetColumnToHeaderMap<T>();

            // 出力対象のプロパティを取得（除外カラムを除く）
            var properties = typeof(T).GetProperties()
                .Where(p => p.GetCustomAttribute<ExcelColumnAttribute>() != null)
                .Where(p => !defaultExcludeColumns.Contains(p.Name))
                .ToList();

            // DBからデータを取得
            var data = GetAllData<T>(tableName);

            if (data.Count == 0)
            {
                Console.WriteLine($"テーブル '{tableName}' にデータがありません。");
                return;
            }

            // Excelファイルを作成
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add(tableName);

            // ヘッダー行を出力
            var headerRow = worksheet.Row(1);
            for (var colIndex = 0; colIndex < properties.Count; colIndex++)
            {
                var prop = properties[colIndex];
                var headerName = columnToHeaderMap.TryGetValue(prop.Name, out var header) ? header : prop.Name;
                var cell = worksheet.Cell(1, colIndex + 1);
                cell.Value = headerName;
            }

            // ヘッダー行のスタイル設定
            var headerRange = worksheet.Range(1, 1, 1, properties.Count);
            headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#4472C4");
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Font.FontColor = XLColor.White;
            headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            headerRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

            // データ行を出力
            for (var rowIndex = 0; rowIndex < data.Count; rowIndex++)
            {
                var item = data[rowIndex];

                for (var colIndex = 0; colIndex < properties.Count; colIndex++)
                {
                    var prop = properties[colIndex];
                    var value = prop.GetValue(item)?.ToString() ?? string.Empty;

                    // 値変換ロジックが指定されていれば適用
                    if (valueTransformer != null)
                    {
                        value = valueTransformer(item, prop.Name, value);
                    }

                    var cell = worksheet.Cell(rowIndex + 2, colIndex + 1);
                    cell.Value = value;
                }
            }

            // データ行全体で折り返して表示を有効化
            if (data.Count > 0)
            {
                var dataBodyRange = worksheet.Range(2, 1, data.Count + 1, properties.Count);
                dataBodyRange.Style.Alignment.WrapText = true;
            }

            // データ範囲全体に枠線を設定
            var dataRange = worksheet.Range(1, 1, data.Count + 1, properties.Count);
            dataRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            dataRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            dataRange.Style.Border.OutsideBorderColor = XLColor.Black;
            dataRange.Style.Border.InsideBorderColor = XLColor.Gray;

            // 全セルの上揃え設定
            dataRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;

            // 列幅を自動調整（最大幅を制限）
            foreach (var column in worksheet.ColumnsUsed())
            {
                column.AdjustToContents();
                if (column.Width > 50)
                {
                    column.Width = 50;
                }
            }

            // ヘッダー行を固定
            worksheet.SheetView.FreezeRows(1);

            // カスタマイズコールバックを実行
            workbookCustomizer?.Invoke(workbook, data);

            // ファイルを保存
            workbook.SaveAs(outputFilePath);
            Console.WriteLine($"Excelファイルを出力しました: {outputFilePath} ({data.Count} 件)");
        }

        /// <summary>
        /// モデルクラスからプロパティ名→日本語ヘッダー名のマッピングを取得
        /// </summary>
        private static Dictionary<string, string> GetColumnToHeaderMap<T>()
        {
            var map = new Dictionary<string, string>();
            var properties = typeof(T).GetProperties()
                .Where(p => p.GetCustomAttribute<ExcelColumnAttribute>() != null);

            foreach (var prop in properties)
            {
                var attr = prop.GetCustomAttribute<ExcelColumnAttribute>()!;
                map[prop.Name] = attr.HeaderName;
            }

            return map;
        }

        /// <summary>
        /// カラムマッピング情報を保持するレコード
        /// </summary>
        private record ColumnMappingInfo(
            Dictionary<string, string> ColumnMappings,
            List<(string excelHeader, string columnName, bool isFurigana)> UpsertKeys
        );
    }
}
