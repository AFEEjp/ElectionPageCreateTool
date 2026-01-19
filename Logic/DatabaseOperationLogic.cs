using System.Data.SQLite;
using System.Reflection;
using ElectionPageCreateTool.Attributes;
using Microsoft.Extensions.Configuration;

namespace ElectionPageCreateTool.Logic
{
    public class DatabaseOperationLogic
    {
        private readonly string _dbFilePath;
        private readonly string _tableName;

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

            _tableName = configuration["DatabaseSettings:TableName"]
                ?? throw new InvalidOperationException("DatabaseSettings:TableName is not configured in appsettings.json");
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
            var excelKeys = data[0].Keys.ToList();

            // カラムマッピングを適用（マッピングがない場合はExcelのキー名をそのまま使用）
            var columnDefinitions = excelKeys
                .Select(key => mappingInfo.ColumnMappings.TryGetValue(key, out var mappedName) ? mappedName : key)
                .ToList();

            using var connection = new SQLiteConnection(GetConnectionString());
            connection.Open();

            using var transaction = connection.BeginTransaction();

            try
            {
                // テーブル作成（存在しない場合）
                CreateTable(connection, tableName, columnDefinitions);

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
        /// テーブルを作成（created_at, updated_at付き）
        /// </summary>
        private void CreateTable(SQLiteConnection connection, string tableName, List<string> columnNames)
        {
            var columns = string.Join(", ", columnNames.Select(c => $"[{c}] TEXT"));
            var createTableSql = $"CREATE TABLE IF NOT EXISTS [{tableName}] (id INTEGER PRIMARY KEY AUTOINCREMENT, {columns}, created_at TEXT, updated_at TEXT)";

            using var command = connection.CreateCommand();
            command.CommandText = createTableSql;
            command.ExecuteNonQuery();

            Console.WriteLine($"テーブル '{tableName}' を作成しました。カラム: {string.Join(", ", columnNames)}, created_at, updated_at");
        }

        /// <summary>
        /// データをUpsert（追加または更新）
        /// </summary>
        private (int insertedCount, int updatedCount, int skippedCount) UpsertData(SQLiteConnection connection, string tableName,
            List<Dictionary<string, string>> data, List<string> excelKeys, List<string> columnNames, ColumnMappingInfo mappingInfo)
        {
            var insertedCount = 0;
            var updatedCount = 0;
            var skippedCount = 0;
            var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            foreach (var row in data)
            {
                // 既存レコードを検索（データも取得）
                var existingRecord = FindExistingRecordWithData(connection, tableName, row, columnNames, mappingInfo);

                if (existingRecord.HasValue)
                {
                    // 値が変更されているか確認
                    if (HasChanges(row, existingRecord.Value.data, excelKeys, columnNames))
                    {
                        // 更新
                        UpdateRecord(connection, tableName, existingRecord.Value.id, row, excelKeys, columnNames, now);
                        updatedCount++;

                        // 更新された項目を表示
                        var keyValues = mappingInfo.UpsertKeys
                            .Select(k => row.TryGetValue(k.excelHeader, out var v) ? v : "")
                            .ToList();
                        Console.WriteLine($"  [更新] {string.Join(" / ", keyValues)}");
                    }
                    else
                    {
                        // 変更なしのためスキップ
                        skippedCount++;
                    }
                }
                else
                {
                    // 挿入
                    InsertRecord(connection, tableName, row, excelKeys, columnNames, now);
                    insertedCount++;
                }
            }

            return (insertedCount, updatedCount, skippedCount);
        }

        /// <summary>
        /// ExcelデータとDBデータを比較して変更があるか確認
        /// </summary>
        private bool HasChanges(Dictionary<string, string> excelRow, Dictionary<string, string> dbRow,
            List<string> excelKeys, List<string> columnNames)
        {
            for (var i = 0; i < excelKeys.Count; i++)
            {
                var excelKey = excelKeys[i];
                var dbColumn = columnNames[i];

                var excelValue = excelRow.GetValueOrDefault(excelKey, "");
                var dbValue = dbRow.GetValueOrDefault(dbColumn, "");

                if (excelValue != dbValue)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 既存レコードを検索してデータも取得
        /// </summary>
        private (long id, Dictionary<string, string> data)? FindExistingRecordWithData(SQLiteConnection connection, string tableName,
            Dictionary<string, string> row, List<string> columnNames, ColumnMappingInfo mappingInfo)
        {
            var whereConditions = new List<string>();
            var parameters = new List<SQLiteParameter>();

            for (int i = 0; i < mappingInfo.UpsertKeys.Count; i++)
            {
                var (excelHeader, columnName, isFurigana) = mappingInfo.UpsertKeys[i];

                if (row.TryGetValue(excelHeader, out var value))
                {
                    // ふりがなカラムの場合はスペースを除去して比較
                    if (isFurigana)
                    {
                        var normalizedValue = NormalizeForComparison(value);
                        whereConditions.Add($"REPLACE(REPLACE([{columnName}], ' ', ''), '　', '') = @key{i}");
                        parameters.Add(new SQLiteParameter($"@key{i}", normalizedValue));
                    }
                    else
                    {
                        whereConditions.Add($"[{columnName}] = @key{i}");
                        parameters.Add(new SQLiteParameter($"@key{i}", value));
                    }
                }
            }

            if (whereConditions.Count == 0)
            {
                return null;
            }

            // IDと全カラムを取得
            var selectColumns = string.Join(", ", new[] { "id" }.Concat(columnNames.Select(c => $"[{c}]")));
            var selectSql = $"SELECT {selectColumns} FROM [{tableName}] WHERE {string.Join(" AND ", whereConditions)} LIMIT 1";

            using var command = connection.CreateCommand();
            command.CommandText = selectSql;
            command.Parameters.AddRange(parameters.ToArray());

            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                var id = reader.GetInt64(0);
                var data = new Dictionary<string, string>();

                for (int i = 0; i < columnNames.Count; i++)
                {
                    var value = reader.IsDBNull(i + 1) ? "" : reader.GetString(i + 1);
                    data[columnNames[i]] = value;
                }

                return (id, data);
            }

            return null;
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
        /// テーブル名を取得
        /// </summary>
        public string GetTableName()
        {
            return _tableName;
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
