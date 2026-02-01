using System.Text;
using System.Text.Json;
using ClosedXML.Excel;
using ElectionPageCreateTool.Extensions;
using ElectionPageCreateTool.Model;
using ElectionPageCreateTool.Model.Shuinsen2026;
using Microsoft.Extensions.Configuration;

namespace ElectionPageCreateTool.Logic.ElectionLogic;

public static class Shuinsen2026Logic
{
    public static readonly DateTime NoticeDate = new DateTime(2026, 1, 27);

    /// <summary>
    /// Q1選択肢とアルファベットの対応（アンケートフォームの順番）
    /// </summary>
    private static readonly Dictionary<string, string> Q1ChoiceOrder = new()
    {
        { "法律で禁止するべき", "A" },
        { "法律で禁止するべきではない", "B" },
        { "どちらともいえない、答えない", "C" }
    };

    /// <summary>
    /// Q2選択肢とアルファベットの対応（アンケートフォームの順番）
    /// </summary>
    private static readonly Dictionary<string, string> Q2ChoiceOrder = new()
    {
        { "刑法のわいせつ物頒布等罪による規制", "A" },
        { "AV新法による規制", "B" },
        { "クレジットカード決済の制約", "C" },
        { "ジェンダー平等論や多様性配慮に基づく表現制限", "D" },
        { "新サイバー犯罪条約による創作物への規制", "E" },
        { "「不適切」を理由とする広告の法的規制", "F" },
        { "AI・機械学習に対する法的規制", "G" },
        { "国旗損壊罪による創作表現規制の懸念", "H" },
        { "表現の自由を不当に制限していると思うものは特にない", "I" }
    };

    public static void MainLogic()
    {
        while (true)
        {
            Console.Clear();

            Console.WriteLine("処理を選択してください:");
            Console.WriteLine("1. ExcelファイルをSQLiteに保存");
            Console.WriteLine("2. サイト生成");
            Console.WriteLine("3. データをExcelに出力");
            Console.WriteLine("9. 終了");

            var choice = Console.ReadLine();

            switch (choice)
            {
                case "1":
                    ImportExcelToDatabase();
                    break;

                case "2":
                    Generate();
                    break;

                case "3":
                    ExportFromDatabase();
                    break;

                case "9":
                    Console.WriteLine("プログラムを終了します。");
                    return;
            }
        }
    }

    private static void ImportExcelToDatabase()
    {
        var excelOperationLogic = new ExcelOperationLogic();

        Console.WriteLine("Excelファイルのパスを入力してください:");
        var excelPath = Console.ReadLine();
        if (!string.IsNullOrEmpty(excelPath))
        {
            var dic = excelOperationLogic.ConvertExcelToDictionary(excelPath);

            Console.WriteLine($"Excelから {dic.Count} 件のデータを読み込みました。");

            // Excel内の重複レコードを排除（同一人物の複数回答は最新のみ採用）
            dic = DeduplicateExcelData(dic);

            if (dic.Count == 0)
            {
                Console.WriteLine("処理対象のデータがありません。");
                Console.ReadKey();
                return;
            }

            Console.WriteLine($"重複排除後: {dic.Count} 件");

            // DBから現在のデータを取得（無回答確認スキップ判定用）
            var dbLogic = new DatabaseOperationLogic();
            var tableName = nameof(Shuinsen2026Respond);
            var currentDbData = dbLogic.GetAllData<Shuinsen2026Respond>(tableName);

            // 無回答の正規化処理
            NormalizeNoAnswerReasons(dic, currentDbData);

            // Reasonが「無回答」の場合、対応するAnswerも「無回答」に置き換え
            ReplaceAnswerForNoAnswerReason(dic);

            // 修正ルールの適用（corrections.jsonに定義された最終調整）
            dic = ApplyCorrections(dic);

            Console.WriteLine($"データベースファイル: {dbLogic.GetDbFilePath()}");
            Console.WriteLine($"テーブル名: {tableName}");
            dbLogic.ShowUpsertKeyColumns<Shuinsen2026Respond>();

            dbLogic.UpsertDataToSQLite<Shuinsen2026Respond>(tableName, dic);
        }

        Console.ReadKey();
    }

    // Excelヘッダー名の定数
    private const string ExcelHeader_Name = "候補者名";
    private const string ExcelHeader_NameFurigana = "候補者名（ふりがな）";
    private const string ExcelHeader_Constituency = "選挙区詳細";
    private const string ExcelHeader_PartyName = "公認を受けている政党・政治団体";
    private const string ExcelHeader_Q1Answer = "【Q1】未成年に見えるキャラクターに対する性的・暴力的な表現があるマンガ・アニメ・ゲームなどの作品の創作・公開・所持について、どのような対応が望ましいと考えますか？";
    private const string ExcelHeader_Q1Reason = "Q1の選択肢を選んだ理由を100文字以内で回答してください";
    private const string ExcelHeader_Q2Answer = "【Q2】以下の項目のうち、「表現の自由を必要以上に損ねている」または「表現の自由に過度に抵触する可能性がある」と思うものをすべてお選びください（複数選択可）";
    private const string ExcelHeader_Q2Reason = "Q2の選択肢を選んだ理由、もしくはその他問題があると思われる規制・懸念があれば100文字以内で回答してください";

    /// <summary>
    /// Reasonが「無回答」の場合、対応するAnswerも「無回答」に置き換える
    /// </summary>
    private static void ReplaceAnswerForNoAnswerReason(List<Dictionary<string, string>> data)
    {
        foreach (var row in data)
        {
            var q1Reason = row.GetValueOrDefault(ExcelHeader_Q1Reason, "");
            if (q1Reason == "無回答" && row.ContainsKey(ExcelHeader_Q1Answer))
            {
                row[ExcelHeader_Q1Answer] = "無回答";
            }

            var q2Reason = row.GetValueOrDefault(ExcelHeader_Q2Reason, "");
            if (q2Reason == "無回答" && row.ContainsKey(ExcelHeader_Q2Answer))
            {
                row[ExcelHeader_Q2Answer] = "無回答";
            }
        }
    }

    /// <summary>
    /// 修正ルールをJSONファイルから読み込んで適用する
    /// </summary>
    /// <param name="data">Excelから読み込んだデータ</param>
    /// <returns>修正適用後のデータ</returns>
    private static List<Dictionary<string, string>> ApplyCorrections(List<Dictionary<string, string>> data)
    {
        var correctionsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "corrections.json");
        if (!File.Exists(correctionsPath))
        {
            return data;
        }

        try
        {
            var jsonContent = File.ReadAllText(correctionsPath);
            using var document = JsonDocument.Parse(jsonContent);

            if (!document.RootElement.TryGetProperty("Corrections", out var corrections))
            {
                return data;
            }

            var appliedCount = 0;

            foreach (var correction in corrections.EnumerateArray())
            {
                if (!correction.TryGetProperty("Match", out var matchConditions) ||
                    !correction.TryGetProperty("Replace", out var replaceValues))
                {
                    continue;
                }

                // 各データ行に対してマッチングを確認
                foreach (var row in data)
                {
                    var isMatch = true;

                    // すべてのMatch条件を確認
                    foreach (var condition in matchConditions.EnumerateObject())
                    {
                        var key = condition.Name;
                        var expectedValue = condition.Value.GetString() ?? string.Empty;

                        if (!row.TryGetValue(key, out var actualValue) ||
                            actualValue?.Trim() != expectedValue.Trim())
                        {
                            isMatch = false;
                            break;
                        }
                    }

                    // マッチした場合、Replace値を適用
                    if (isMatch)
                    {
                        var name = row.GetValueOrDefault(ExcelHeader_Name, "");
                        var replacedFields = new List<string>();

                        foreach (var replace in replaceValues.EnumerateObject())
                        {
                            var key = replace.Name;
                            var newValue = replace.Value.GetString() ?? string.Empty;

                            if (row.ContainsKey(key))
                            {
                                var oldValue = row[key];
                                if (oldValue != newValue)
                                {
                                    row[key] = newValue;
                                    replacedFields.Add($"{key}: 「{oldValue}」→「{newValue}」");
                                }
                            }
                        }

                        if (replacedFields.Count > 0)
                        {
                            appliedCount++;
                            Console.WriteLine($"  修正適用: {name}");
                            foreach (var field in replacedFields)
                            {
                                Console.WriteLine($"    {field}");
                            }
                        }
                    }
                }
            }

            if (appliedCount > 0)
            {
                Console.WriteLine($"修正ルール適用: {appliedCount} 件");
            }
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"corrections.json の解析エラー: {ex.Message}");
        }

        return data;
    }

    /// <summary>
    /// Excel内の重複レコードを排除し、最新（最後）のレコードのみを残す
    /// </summary>
    /// <param name="data">Excelから読み込んだデータ</param>
    /// <returns>重複排除後のデータ</returns>
    private static List<Dictionary<string, string>> DeduplicateExcelData(List<Dictionary<string, string>> data)
    {
        // Upsertキー（ふりがな+選挙区+政党）でグループ化
        var grouped = data
            .Select((row, index) => (row, index))
            .GroupBy(x => (
                Furigana: x.row.GetValueOrDefault(ExcelHeader_NameFurigana, "")?.Trim() ?? "",
                Constituency: x.row.GetValueOrDefault(ExcelHeader_Constituency, "")?.Trim() ?? "",
                Party: x.row.GetValueOrDefault(ExcelHeader_PartyName, "")?.Trim() ?? ""
            ))
            .ToList();

        var duplicates = grouped.Where(g => g.Count() > 1).ToList();

        if (duplicates.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("========================================");
            Console.WriteLine("【重複レコードの検出】");
            Console.WriteLine("同一人物の複数回答が検出されました。最新の回答を採用します。");
            Console.WriteLine("========================================");

            foreach (var dup in duplicates)
            {
                var records = dup.OrderBy(x => x.index).ToList();
                var latest = records.Last();
                var name = latest.row.GetValueOrDefault(ExcelHeader_Name, "");

                Console.WriteLine($"  {name}（{dup.Key.Furigana}）- {dup.Key.Constituency} - {dup.Key.Party}");
                Console.WriteLine($"    → {records.Count}件中、最新の回答（行{latest.index + 2}）を採用");
            }

            Console.WriteLine("========================================");
            Console.WriteLine();
        }

        // 各グループの最後のレコード（最新）を取得し、元の順序を維持
        var deduplicatedData = grouped
            .Select(g => g.OrderBy(x => x.index).Last())
            .OrderBy(x => x.index)
            .Select(x => x.row)
            .ToList();

        return deduplicatedData;
    }

    /// <summary>
    /// 無回答判定の結果
    /// </summary>
    private enum NoAnswerCheckResult
    {
        NotNoAnswer,      // 無回答ではない
        ExactNoAnswer,    // 完全一致で無回答
        PartialNoAnswer   // 部分一致（確認が必要）
    }

    /// <summary>
    /// Reasonフィールドの無回答チェック
    /// </summary>
    private static NoAnswerCheckResult CheckNoAnswerReason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return NoAnswerCheckResult.NotNoAnswer;

        var trimmed = reason.Trim();
        if (trimmed == "無回答")
            return NoAnswerCheckResult.ExactNoAnswer;

        if (trimmed.Contains("無回答"))
            return NoAnswerCheckResult.PartialNoAnswer;

        return NoAnswerCheckResult.NotNoAnswer;
    }

    /// <summary>
    /// 無回答を含むReasonの正規化処理
    /// </summary>
    /// <param name="data">Excelから読み込んだデータ</param>
    /// <param name="currentDbData">DBの現在データ（確認スキップ判定用）</param>
    private static void NormalizeNoAnswerReasons(List<Dictionary<string, string>> data, List<Shuinsen2026Respond> currentDbData)
    {
        // DBデータをふりがなでインデックス化（高速検索用）
        var dbByFurigana = currentDbData
            .GroupBy(r => r.NameFurigana?.Trim() ?? "")
            .ToDictionary(g => g.Key, g => g.ToList());

        var needsUserConfirmation = new List<(Dictionary<string, string> row, bool q1NeedsConfirm, bool q2NeedsConfirm)>();

        // 部分一致かつDBで未正規化のものを抽出
        foreach (var row in data)
        {
            var nameFurigana = row.GetValueOrDefault(ExcelHeader_NameFurigana, "")?.Trim() ?? "";
            var q1Reason = row.GetValueOrDefault(ExcelHeader_Q1Reason, "");
            var q2Reason = row.GetValueOrDefault(ExcelHeader_Q2Reason, "");

            var q1Check = CheckNoAnswerReason(q1Reason);
            var q2Check = CheckNoAnswerReason(q2Reason);

            var q1NeedsConfirm = false;
            var q2NeedsConfirm = false;

            // Q1の部分一致チェック
            if (q1Check == NoAnswerCheckResult.PartialNoAnswer)
            {
                // DBに該当レコードがあり、既に「無回答」なら自動正規化
                if (dbByFurigana.TryGetValue(nameFurigana, out var dbRecords) &&
                    dbRecords.Any(r => r.Q1Reason == "無回答"))
                {
                    row[ExcelHeader_Q1Reason] = "無回答";
                }
                else
                {
                    q1NeedsConfirm = true;
                }
            }

            // Q2の部分一致チェック
            if (q2Check == NoAnswerCheckResult.PartialNoAnswer)
            {
                // DBに該当レコードがあり、既に「無回答」なら自動正規化
                if (dbByFurigana.TryGetValue(nameFurigana, out var dbRecords) &&
                    dbRecords.Any(r => r.Q2Reason == "無回答"))
                {
                    row[ExcelHeader_Q2Reason] = "無回答";
                }
                else
                {
                    q2NeedsConfirm = true;
                }
            }

            if (q1NeedsConfirm || q2NeedsConfirm)
            {
                needsUserConfirmation.Add((row, q1NeedsConfirm, q2NeedsConfirm));
            }
        }

        if (needsUserConfirmation.Count == 0)
        {
            return;
        }

        Console.WriteLine();
        Console.WriteLine("========================================");
        Console.WriteLine("【無回答の確認】");
        Console.WriteLine("「無回答」を含むが完全一致ではない回答があります。");
        Console.WriteLine("（DBで既に正規化済みのものは自動スキップ）");
        Console.WriteLine("========================================");

        foreach (var (row, q1NeedsConfirm, q2NeedsConfirm) in needsUserConfirmation)
        {
            var name = row.GetValueOrDefault(ExcelHeader_Name, "");
            var nameFurigana = row.GetValueOrDefault(ExcelHeader_NameFurigana, "");

            Console.WriteLine();
            Console.WriteLine($"----------------------------------------");
            Console.WriteLine($"候補者: {name}（{nameFurigana}）");
            Console.WriteLine($"----------------------------------------");

            // Q1Reasonの確認
            if (q1NeedsConfirm)
            {
                var q1Reason = row.GetValueOrDefault(ExcelHeader_Q1Reason, "");
                Console.WriteLine($"Q1Reason: 「{q1Reason}」");
                Console.Write("  → 「無回答」として扱いますか？ (Y/N): ");
                var input = Console.ReadLine()?.Trim().ToUpper();
                if (input == "Y")
                {
                    row[ExcelHeader_Q1Reason] = "無回答";
                    Console.WriteLine("  → 「無回答」に正規化しました。");
                }
                else
                {
                    Console.WriteLine("  → そのまま保持します。");
                }
            }

            // Q2Reasonの確認
            if (q2NeedsConfirm)
            {
                var q2Reason = row.GetValueOrDefault(ExcelHeader_Q2Reason, "");
                Console.WriteLine($"Q2Reason: 「{q2Reason}」");
                Console.Write("  → 「無回答」として扱いますか？ (Y/N): ");
                var input = Console.ReadLine()?.Trim().ToUpper();
                if (input == "Y")
                {
                    row[ExcelHeader_Q2Reason] = "無回答";
                    Console.WriteLine("  → 「無回答」に正規化しました。");
                }
                else
                {
                    Console.WriteLine("  → そのまま保持します。");
                }
            }
        }

        Console.WriteLine();
        Console.WriteLine("========================================");
        Console.WriteLine("無回答の確認が完了しました。");
        Console.WriteLine("========================================");
        Console.WriteLine();
    }

    /// <summary>
    /// 出力先ディレクトリを取得する共通メソッド
    /// </summary>
    /// <returns>出力先ディレクトリパス。取得できない場合はnull</returns>
    private static string? GetOutputDirectoryPath()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        var directoryPath = configuration["OutputSettings:DirectoryPath"];

        if (string.IsNullOrEmpty(directoryPath))
        {
            Console.WriteLine("出力先ディレクトリを入力してください:");
            directoryPath = Console.ReadLine();

            if (string.IsNullOrEmpty(directoryPath))
            {
                Console.WriteLine("出力先ディレクトリが指定されていません。");
                return null;
            }
        }
        else
        {
            Console.WriteLine($"出力先ディレクトリ: {directoryPath}");
        }

        if (!Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        return directoryPath;
    }

    private static void ExportFromDatabase()
    {
        var directoryPath = GetOutputDirectoryPath();
        if (directoryPath == null)
        {
            return;
        }

        var dbLogic = new DatabaseOperationLogic();
        var tableName = nameof(Shuinsen2026Respond);

        // ファイル名: テーブル名_yyyyMMddHHmmss.xlsx
        var fileName = $"{tableName}_{DateTime.Now:yyyyMMddHHmmss}.xlsx";
        var outputFilePath = Path.Combine(directoryPath, fileName);

        dbLogic.ExportToExcel<Shuinsen2026Respond>(
            tableName: tableName,
            outputFilePath: outputFilePath,
            excludeColumns: ["Result"],
            valueTransformer: ApplyNoAnswerRule,
            workbookCustomizer: AddQ2FilterSheet);

        Console.ReadKey();
    }

    /// <summary>
    /// Q2選択肢フィルター用シートを追加
    /// </summary>
    private static void AddQ2FilterSheet(IXLWorkbook workbook, List<Shuinsen2026Respond> data)
    {
        var worksheet = workbook.Worksheets.Add("Q2フィルター");

        // ヘッダー行を作成
        var headers = new List<string>
        {
            "候補者名",
            "選挙区",
            "政党",
            "A. 刑法のわいせつ物頒布等罪による規制",
            "B. AV新法による規制",
            "C. クレジットカード決済の制約",
            "D. ジェンダー平等論や多様性配慮に基づく表現制限",
            "E. 新サイバー犯罪条約による創作物への規制",
            "F. 「不適切」を理由とする広告の法的規制",
            "G. AI・機械学習に対する法的規制",
            "H. 国旗損壊罪による創作表現規制の懸念",
            "I. 表現の自由を不当に制限していると思うものは特にない",
            "無回答"
        };

        // Q2選択肢のリスト（チェック用）
        var q2Choices = Q2ChoiceOrder.Keys.ToList();

        // ヘッダー出力
        for (var i = 0; i < headers.Count; i++)
        {
            worksheet.Cell(1, i + 1).Value = headers[i];
        }

        // ヘッダースタイル
        var headerRange = worksheet.Range(1, 1, 1, headers.Count);
        headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#4472C4");
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Font.FontColor = XLColor.White;
        headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        headerRange.Style.Alignment.WrapText = true;

        // データ行を出力
        for (var rowIndex = 0; rowIndex < data.Count; rowIndex++)
        {
            var item = data[rowIndex];
            var row = rowIndex + 2;

            worksheet.Cell(row, 1).Value = item.Name;
            worksheet.Cell(row, 2).Value = item.Constituency;
            worksheet.Cell(row, 3).Value = item.PartyName;

            // 無回答チェック
            var isNoAnswer = item.Q2Reason == "無回答";

            if (isNoAnswer)
            {
                // 無回答の場合は最後の列に○
                worksheet.Cell(row, headers.Count).Value = "○";
            }
            else
            {
                // Q2Answerをカンマで分割
                var selectedChoices = item.Q2Answer
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(a => a.Trim())
                    .ToHashSet();

                // 各選択肢に○を設定
                for (var choiceIndex = 0; choiceIndex < q2Choices.Count; choiceIndex++)
                {
                    if (selectedChoices.Contains(q2Choices[choiceIndex]))
                    {
                        worksheet.Cell(row, choiceIndex + 4).Value = "○";
                    }
                }
            }
        }

        // 枠線設定
        var dataRange = worksheet.Range(1, 1, data.Count + 1, headers.Count);
        dataRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        dataRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        dataRange.Style.Border.OutsideBorderColor = XLColor.Black;
        dataRange.Style.Border.InsideBorderColor = XLColor.Gray;

        // 選択肢列を中央揃え
        var choiceRange = worksheet.Range(2, 4, data.Count + 1, headers.Count);
        choiceRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        // 列幅調整
        worksheet.Column(1).Width = 15;  // 候補者名
        worksheet.Column(2).Width = 20;  // 選挙区
        worksheet.Column(3).Width = 15;  // 政党
        for (var i = 4; i <= headers.Count; i++)
        {
            worksheet.Column(i).Width = 8;  // 選択肢列
        }

        // ヘッダー行を固定
        worksheet.SheetView.FreezeRows(1);

        // オートフィルター設定
        worksheet.RangeUsed()?.SetAutoFilter();

        Console.WriteLine("Q2フィルターシートを追加しました。");
    }

    /// <summary>
    /// Excel出力用の値変換ロジック
    /// Q2Answerはアルファベット付きで改行区切りにフォーマット
    /// 生年月日は日付のみにフォーマット
    /// </summary>
    private static string ApplyNoAnswerRule(Shuinsen2026Respond item, string propertyName, string value)
    {
        return propertyName switch
        {
            "Q2Answer" => FormatQ2AnswerForExcel(value),
            "Birthdate" => FormatDateOnly(value),
            _ => value
        };
    }

    /// <summary>
    /// 日付文字列から日付部分のみを取得
    /// </summary>
    private static string FormatDateOnly(string dateValue)
    {
        if (string.IsNullOrWhiteSpace(dateValue))
        {
            return string.Empty;
        }

        if (DateTime.TryParse(dateValue, out var date))
        {
            return date.ToString("yyyy/MM/dd");
        }

        return dateValue;
    }

    /// <summary>
    /// 設問（2-a）の回答をExcel用にフォーマット（アルファベット付き、改行区切り）
    /// </summary>
    private static string FormatQ2AnswerForExcel(string q2Answer)
    {
        if (string.IsNullOrWhiteSpace(q2Answer))
        {
            return string.Empty;
        }

        // カンマで分割して各回答を取得
        var answers = q2Answer.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(a => a.Trim())
            .Where(a => !string.IsNullOrEmpty(a))
            .ToList();

        if (answers.Count == 0)
        {
            return string.Empty;
        }

        // アンケートの順番でソート
        var sortedAnswers = answers
            .OrderBy(a => Q2ChoiceOrder.TryGetValue(a, out var order) ? order : "Z")
            .ToList();

        // アルファベット付きで改行区切りにフォーマット
        var formattedItems = sortedAnswers
            .Select(a => Q2ChoiceOrder.TryGetValue(a, out var prefix) ? $"{prefix}. {a}" : a);

        return string.Join("\n", formattedItems);
    }

    private static void Generate()
    {
        var directoryPath = GetOutputDirectoryPath();
        if (directoryPath == null)
        {
            return;
        }

        // DBからデータを取得
        var dbLogic = new DatabaseOperationLogic();
        var tableName = nameof(Shuinsen2026Respond);
        var list = dbLogic.GetAllData<Shuinsen2026Respond>(tableName);

        if (list.Count == 0)
        {
            Console.WriteLine("データがありません。");
            return;
        }

        // ふりがなの値を利用して並び替え
        list = list.OrderBy(r => r.NameFurigana).ToList();

        // 選挙区の一覧を取得
        var 小選挙区Dic = new Dictionary<Prefecture, List<(string EnStr, string JpStr)>>();
        foreach (var pref in Enum.GetValues<Prefecture>())
        {
            var counts = pref.GetShugiinKuwariCount();

            var 小選挙区List = new List<(string EnStr, string JpStr)>();
            for (var i = 1; i <= counts; i++)
            {
                小選挙区List.Add(($"{pref.ToString().ToLower()}-{i}", $"{pref.GetShugiinKuwariStr()}{i}区"));
            }
            小選挙区Dic.Add(pref, 小選挙区List);
        }

        // 小選挙区ごとに候補者情報を分ける
        var 小選挙区出力内容Dic = new List<(Prefecture 都道府県名, Dictionary<(string EnStr, string JpStr), List<Shuinsen2026Respond>> 候補者情報リスト)>();
        foreach (var kv in 小選挙区Dic)
        {
            var kuwariList = kv.Value;

            var 区分けDic = new Dictionary<(string EnStr, string JpStr), List<Shuinsen2026Respond>>();
            foreach (var kuwari in kuwariList)
            {
                var respList = list.Where(r => r.Constituency == kuwari.JpStr).ToList();

                // 取得した分を元のリストから削除
                foreach (var item in respList)
                {
                    list.Remove(item);
                }
                区分けDic.Add(kuwari, respList);
            }

            小選挙区出力内容Dic.Add((kv.Key, 区分けDic));
        }

        var strBldr = new StringBuilder();

        foreach (var vt in 小選挙区出力内容Dic)
        {
            strBldr.AppendLine($"<div class='district-section'>");
            strBldr.AppendLine($"<h3 id='pref-{vt.都道府県名.ToString().ToLower()}'>{vt.都道府県名.GetDescription()}</h3>");
            strBldr.AppendLine("<div>");

            foreach (var kv in vt.候補者情報リスト)
            {
                strBldr.AppendLine($"<div class='kuwari-section'>");
                strBldr.AppendLine($"<h4 id='kuwari-{kv.Key.EnStr}'>{kv.Key.JpStr}</h4>");
                strBldr.AppendLine("<div class='kohosya-info'><ol class='distList'>");

                foreach (var resp in kv.Value)
                {
                    strBldr.AppendCandidateInfoHtml(resp);
                }

                strBldr.AppendLine("</ol></div>");
                strBldr.AppendLine("</div>");
            }

            strBldr.AppendLine("</div>");
            strBldr.AppendLine("</div>");
        }
        var 小選挙区出力 = strBldr.ToString();

        File.WriteAllText(Path.Combine(directoryPath, "single-seat.html"), 小選挙区出力);

        Console.WriteLine("小選挙区HTMLを出力しました。");

        // prefectureフォルダに都道府県単位でHTMLを出力
        var prefectureDirectoryPath = Path.Combine(directoryPath, "prefecture");
        if (!Directory.Exists(prefectureDirectoryPath))
        {
            Directory.CreateDirectory(prefectureDirectoryPath);
        }

        foreach (var vt in 小選挙区出力内容Dic)
        {
            var prefStrBldr = new StringBuilder();
            prefStrBldr.AppendLine($"<div class='district-section'>");
            prefStrBldr.AppendLine($"<h3 id='pref-{vt.都道府県名.ToString().ToLower()}'>{vt.都道府県名.GetDescription()}</h3>");
            prefStrBldr.AppendLine("<div>");

            foreach (var kv in vt.候補者情報リスト)
            {
                prefStrBldr.AppendLine($"<div class='kuwari-section'>");
                prefStrBldr.AppendLine($"<h4 id='kuwari-{kv.Key.EnStr}'>{kv.Key.JpStr}</h4>");
                prefStrBldr.AppendLine("<div class='kohosya-info'><ol class='distList'>");

                foreach (var resp in kv.Value)
                {
                    prefStrBldr.AppendCandidateInfoHtml(resp);
                }

                prefStrBldr.AppendLine("</ol></div>");
                prefStrBldr.AppendLine("</div>");
            }

            prefStrBldr.AppendLine("</div>");
            prefStrBldr.AppendLine("</div>");

            var prefFileName = $"{vt.都道府県名.ToString().ToLower()}.html";
            File.WriteAllText(Path.Combine(prefectureDirectoryPath, prefFileName), prefStrBldr.ToString());
        }

        Console.WriteLine($"Liteフォルダに都道府県別HTMLを出力しました。（{小選挙区出力内容Dic.Count}件）");

        strBldr.Clear();

        // 比例ブロックごとに候補者情報を分ける
        var removeRecords = new List<Shuinsen2026Respond>();
        var 比例ブロック出力内容Dic = new Dictionary<ShuinHireiBlock2026, List<Shuinsen2026Respond>>();
        foreach (var blk in Enum.GetValues<ShuinHireiBlock2026>())
        {
            var blockName = blk.GetDescription();

            var respondList = new List<Shuinsen2026Respond>();
            foreach (var respond in list.Where(r => r.Constituency == blockName))
            {
                removeRecords.Add(respond);
                respondList.Add(respond);
            }

            // 取得した分を元のリストから削除
            foreach (var respond in removeRecords)
            {
                list.Remove(respond);
            }

            比例ブロック出力内容Dic.Add(blk, respondList);
        }

        // 比例ブロックの出力
        foreach (var kv in 比例ブロック出力内容Dic)
        {
            strBldr.AppendLine($"<div class='kuwari-section'>");
            strBldr.AppendLine($"<h3 id='hirei-{kv.Key.ToString().ToLower()}'>{kv.Key.GetDescription()}</h3>");
            strBldr.AppendLine("<div class='kohosya-info'><ol class='distList'>");

            foreach (var resp in kv.Value)
            {
                strBldr.AppendCandidateInfoHtml(resp);
            }

            strBldr.AppendLine("</ol></div>");
            strBldr.AppendLine("</div>");
        }

        var 比例区出力 = strBldr.ToString();

        File.WriteAllText(Path.Combine(directoryPath, "proportional.html"), 比例区出力);

        Console.WriteLine("比例区HTMLを出力しました。");

        // proportionalフォルダに比例ブロック単位でHTMLを出力
        var proportionalDirectoryPath = Path.Combine(directoryPath, "proportional");
        if (!Directory.Exists(proportionalDirectoryPath))
        {
            Directory.CreateDirectory(proportionalDirectoryPath);
        }

        foreach (var kv in 比例ブロック出力内容Dic)
        {
            var blockStrBldr = new StringBuilder();
            blockStrBldr.AppendLine($"<div class='kuwari-section'>");
            blockStrBldr.AppendLine($"<h3 id='hirei-{kv.Key.ToString().ToLower()}'>{kv.Key.GetDescription()}</h3>");
            blockStrBldr.AppendLine("<div class='kohosya-info'><ol class='distList'>");

            foreach (var resp in kv.Value)
            {
                blockStrBldr.AppendCandidateInfoHtml(resp);
            }

            blockStrBldr.AppendLine("</ol></div>");
            blockStrBldr.AppendLine("</div>");

            var blockFileName = $"{kv.Key.ToString().ToLower()}.html";
            File.WriteAllText(Path.Combine(proportionalDirectoryPath, blockFileName), blockStrBldr.ToString());
        }

        Console.WriteLine($"proportionalフォルダに比例ブロック別HTMLを出力しました。（{比例ブロック出力内容Dic.Count}件）");

        Console.WriteLine($"小選挙区、比例区どちらにも該当しないデータ:{list.Count}件");

        foreach (var resp in list)
        {
            Console.WriteLine($"名前: {resp.Name}");
            Console.WriteLine($"ふりがな: {resp.NameFurigana}");
            Console.WriteLine($"誕生日: {resp.Birthdate}");
            Console.WriteLine($"選挙区: {resp.Constituency}");
            Console.WriteLine($"所属政党: {resp.PartyName}");
            Console.WriteLine($"Q1: {resp.Q1Answer}");
            Console.WriteLine($"Q1の理由: {resp.Q1Reason}");
            Console.WriteLine($"Q2: {resp.Q2Answer}");
            Console.WriteLine($"Q2の理由: {resp.Q2Reason}");
            Console.WriteLine($"Q3: {resp.FavoriteMangaAnimeGame}");
            Console.WriteLine();
        }

        Console.ReadKey();
    }

    public static StringBuilder AppendCandidateInfoHtml(this StringBuilder strBuilder, Shuinsen2026Respond resp)
    {
        // Q1/Q2の回答をdata属性用にアルファベットコードに変換
        var q1DataValue = resp.Q1Answer == "無回答"
            ? "無回答"
            : Q1ChoiceOrder.TryGetValue(resp.Q1Answer.Trim(), out var q1Code) ? q1Code : resp.Q1Answer;
        var q2DataValue = resp.Q2Answer == "無回答"
            ? "無回答"
            : ConvertQ2AnswerToCodes(resp.Q2Answer);

        strBuilder.AppendLine($"<li class='candidate-card' data-q1='{System.Web.HttpUtility.HtmlAttributeEncode(q1DataValue)}' data-q2='{System.Web.HttpUtility.HtmlAttributeEncode(q2DataValue)}' data-party='{System.Web.HttpUtility.HtmlAttributeEncode(resp.PartyName)}'><ol><li><dl>");

        var name = $"{resp.Name.Trim().Replace("　", " ")}<span>（{resp.NameFurigana.Trim().Replace("　", " ")}）</span>";
        var hrefAnchor = $"{resp.PartyName}_{resp.Name.Trim().Replace("　", "").Replace(" ", "").Replace("\u3000", "")}";

        strBuilder.AppendLine($"<li class='kohosya-name' id='{hrefAnchor}'>{(resp.Result == "当選" ? "\ud83c\udf38 " : string.Empty)} <a class='name' href='#{hrefAnchor}'>{name}</a> ：\u3000<span>{resp.PartyName}</span></li>");

        // 誕生日
        if (!string.IsNullOrEmpty(resp.Birthdate) && DateTime.TryParse(resp.Birthdate, out var birthdate))
        {
            var age = NoticeDate.Year - birthdate.Year;
            if (birthdate > NoticeDate.AddYears(-age)) age--;
            strBuilder.AppendLine($"<li class='birthdate'><b>誕生日：</b><br>{birthdate:yyyy/MM/dd} （{age}歳）</li>");
        }

        strBuilder.AppendLine($"<li class='setsumon'><b>設問（1-a）：</b>{resp.Q1Answer}</li>");
        strBuilder.AppendLine($"<li class='setsumon'><b>設問（1-b）：</b>{resp.Q1Reason}</li>");

        var q2AnswerDisplay = resp.Q2Answer == "無回答" ? "無回答" : FormatQ2AnswerAsList(resp.Q2Answer);
        strBuilder.AppendLine($"<li class='setsumon'><b>設問（2-a）：</b>{q2AnswerDisplay}</li>");
        strBuilder.AppendLine($"<li class='setsumon'><b>設問（2-b）：</b>{resp.Q2Reason}</li>");
        strBuilder.AppendLine($"<li class='setsumon'><b>設問（3）：</b>{resp.FavoriteMangaAnimeGame}</li>");
        strBuilder.AppendLine("</dl></li></ol></li>");

        return strBuilder;
    }

    /// <summary>
    /// Q2回答をカンマ区切りのアルファベットコードに変換（data属性用）
    /// </summary>
    private static string ConvertQ2AnswerToCodes(string q2Answer)
    {
        if (string.IsNullOrWhiteSpace(q2Answer))
        {
            return string.Empty;
        }

        var answers = q2Answer.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(a => a.Trim())
            .Where(a => !string.IsNullOrEmpty(a))
            .ToList();

        if (answers.Count == 0)
        {
            return string.Empty;
        }

        // アルファベットコードに変換
        var codes = answers
            .Select(a => Q2ChoiceOrder.TryGetValue(a, out var code) ? code : a)
            .OrderBy(c => c);

        return string.Join(",", codes);
    }

    /// <summary>
    /// 設問（2-a）の回答をHTML用にアルファベット付きリストにフォーマット
    /// </summary>
    private static string FormatQ2AnswerAsList(string q2Answer)
    {
        if (string.IsNullOrWhiteSpace(q2Answer))
        {
            return string.Empty;
        }

        // カンマで分割して各回答を取得
        var answers = q2Answer.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(a => a.Trim())
            .Where(a => !string.IsNullOrEmpty(a))
            .ToList();

        if (answers.Count == 0)
        {
            return string.Empty;
        }

        // アンケートの順番でソート
        var sortedAnswers = answers
            .OrderBy(a => Q2ChoiceOrder.TryGetValue(a, out var order) ? order : "Z")
            .ToList();

        // HTMLリストを生成
        var listItems = sortedAnswers
            .Select(a => Q2ChoiceOrder.TryGetValue(a, out var prefix) ? $"<li>{prefix}. {a}</li>" : $"<li>{a}</li>");

        return $"<ul class='q2-list'>{string.Join("", listItems)}</ul>";
    }
}