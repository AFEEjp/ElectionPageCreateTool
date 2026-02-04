using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using ClosedXML.Excel;
using ElectionPageCreateTool.Extensions;
using ElectionPageCreateTool.Model;
using ElectionPageCreateTool.Model.Shuinsen2026;
using Microsoft.Extensions.Configuration;
using OfficeOpenXml;
using OfficeOpenXml.Drawing.Chart;

namespace ElectionPageCreateTool.Logic.ElectionLogic;

public static class Shuinsen2026Logic
{
    public static readonly DateTime NoticeDate = new DateTime(2026, 1, 27);

    /// <summary>
    /// 比例ブロック出力時の政党ソート順
    /// </summary>
    private static readonly Dictionary<string, int> PartySortOrder = new()
    {
        { "自由民主党", 0 },
        { "日本維新の会", 1 },
        { "中道改革連合", 2 },
        { "国民民主党", 3 },
        { "チームみらい", 4 },
        { "参政党", 5 },
        { "日本共産党", 6 },
        { "れいわ新選組", 7 },
        { "日本保守党（代表者：百田尚樹）", 8 },
        { "社会民主党", 9 },
        { "減税日本", 10 },
        { "減税日本・ゆうこく連合", 11 },
        { "再生の道", 12 },
        { "無所属連合", 13 },
        { "無所属", 14 },
    };

    private static int GetPartySortKey(string partyName)
    {
        if (PartySortOrder.TryGetValue(partyName, out var order))
            return order;
        // 定義にない政党は「無所属」の直前（13と14の間 → 13を使うと無所属連合と被るので、無所属の前に入る値）
        return PartySortOrder["無所属"] - 1;
    }

    /// <summary>
    /// 政党名の正規化マッピング（統合先への変換）
    /// </summary>
    private static readonly Dictionary<string, string> PartyNameMapping = new()
    {
        { "減税日本", "減税日本・ゆうこく連合" },
        { "自民党推薦", "無所属" },
    };

    /// <summary>
    /// 政党名を正規化する（統合先があればそちらに変換）
    /// </summary>
    private static string NormalizePartyName(string partyName)
    {
        return PartyNameMapping.GetValueOrDefault(partyName, partyName);
    }

    /// <summary>
    /// 政党別の候補者数（擁立数）
    /// ※「減税日本」は「減税日本・ゆうこく連合」、「自民党推薦」は「無所属」に含まれる
    /// </summary>
    private static readonly Dictionary<string, int> PartyCandidateCount = new()
    {
        { "自由民主党", 337 },
        { "中道改革連合", 236 },
        { "日本維新の会", 89 },
        { "国民民主党", 104 },
        { "れいわ新選組", 31 },
        { "日本共産党", 176 },
        { "減税日本・ゆうこく連合", 18 },
        { "参政党", 190 },
        { "日本保守党（代表者：百田尚樹）", 20 },
        { "世界平和党", 1 },
        { "社会民主党", 15 },
        { "チームみらい", 14 },
        { "再生の道", 2 },
        { "未来進歩党", 1 },
        { "日本自由党", 1 },
        { "無所属連合", 2 },
        { "安楽死制度を考える会", 2 },
        { "一番星", 1 },
        { "核融合党", 1 },
        { "日本大和党", 1 },
        { "無所属", 41 },
    };

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
            var deletedCount = 0;
            var rowsToDelete = new List<Dictionary<string, string>>();

            foreach (var correction in corrections.EnumerateArray())
            {
                if (!correction.TryGetProperty("Match", out var matchConditions))
                {
                    continue;
                }

                // IsDeleteフラグの確認
                var isDelete = correction.TryGetProperty("IsDelete", out var isDeleteProp)
                    && isDeleteProp.GetBoolean();

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

                    // マッチした場合の処理
                    if (isMatch)
                    {
                        var name = row.GetValueOrDefault(ExcelHeader_Name, "");

                        if (isDelete)
                        {
                            // 削除対象としてマーク
                            if (!rowsToDelete.Contains(row))
                            {
                                rowsToDelete.Add(row);
                                deletedCount++;
                                Console.WriteLine($"  削除対象: {name}");
                            }
                        }
                        else
                        {
                            // Replace値を適用
                            if (correction.TryGetProperty("Replace", out var replaceValues))
                            {
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
                }
            }

            // 削除対象を実際に削除
            foreach (var rowToDelete in rowsToDelete)
            {
                data.Remove(rowToDelete);
            }

            if (appliedCount > 0)
            {
                Console.WriteLine($"修正ルール適用: {appliedCount} 件");
            }
            if (deletedCount > 0)
            {
                Console.WriteLine($"データ削除: {deletedCount} 件");
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
            workbookCustomizer: (workbook, data) =>
            {
                AddQ2FilterSheet(workbook, data);
                AddPartyResponseSheet(workbook, data);
                AddQ1ChartDataSheet(workbook, data);
                AddQ2ChartDataSheet(workbook, data);
            });

        // ClosedXMLで保存後、EPPlusでチャートを追加
        AddChartsToExcel(outputFilePath);

        Console.ReadKey();
    }

    /// <summary>
    /// 政党別回答状況シートを追加
    /// </summary>
    private static void AddPartyResponseSheet(IXLWorkbook workbook, List<Shuinsen2026Respond> data)
    {
        var worksheet = workbook.Worksheets.Add("政党別回答状況");

        // ヘッダー
        var headers = new[] { "政党名", "候補者数", "回答数", "未回答数", "回答率" };
        for (var i = 0; i < headers.Length; i++)
        {
            worksheet.Cell(1, i + 1).Value = headers[i];
        }

        // ヘッダースタイル
        var headerRange = worksheet.Range(1, 1, 1, headers.Length);
        headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#4472C4");
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Font.FontColor = XLColor.White;
        headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        // 政党別にグループ化（PartySortOrder順）
        var partyGroups = data
            .GroupBy(d => NormalizePartyName(d.PartyName))
            .OrderBy(g => GetPartySortKey(g.Key))
            .Select(g => new { PartyName = g.Key, ResponseCount = g.Count() })
            .ToList();

        var grandCandidateTotal = 0;
        var grandResponseTotal = 0;

        for (var rowIndex = 0; rowIndex < partyGroups.Count; rowIndex++)
        {
            var party = partyGroups[rowIndex];
            var row = rowIndex + 2;
            var candidateCount = PartyCandidateCount.GetValueOrDefault(party.PartyName, 0);

            var noResponseCount = Math.Max(0, candidateCount - party.ResponseCount);

            worksheet.Cell(row, 1).Value = party.PartyName;
            worksheet.Cell(row, 2).Value = candidateCount;
            worksheet.Cell(row, 3).Value = party.ResponseCount;
            worksheet.Cell(row, 4).Value = noResponseCount;
            worksheet.Cell(row, 5).Value = candidateCount == 0 ? 0 : (double)party.ResponseCount / candidateCount;
            worksheet.Cell(row, 5).Style.NumberFormat.Format = "0.0%";

            grandCandidateTotal += candidateCount;
            grandResponseTotal += party.ResponseCount;
        }

        // 合計行
        var totalRow = partyGroups.Count + 2;
        var grandNoResponseTotal = Math.Max(0, grandCandidateTotal - grandResponseTotal);
        worksheet.Cell(totalRow, 1).Value = "合計";
        worksheet.Cell(totalRow, 1).Style.Font.Bold = true;
        worksheet.Cell(totalRow, 2).Value = grandCandidateTotal;
        worksheet.Cell(totalRow, 2).Style.Font.Bold = true;
        worksheet.Cell(totalRow, 3).Value = grandResponseTotal;
        worksheet.Cell(totalRow, 3).Style.Font.Bold = true;
        worksheet.Cell(totalRow, 4).Value = grandNoResponseTotal;
        worksheet.Cell(totalRow, 4).Style.Font.Bold = true;
        worksheet.Cell(totalRow, 5).Value = grandCandidateTotal == 0 ? 0 : (double)grandResponseTotal / grandCandidateTotal;
        worksheet.Cell(totalRow, 5).Style.NumberFormat.Format = "0.0%";
        worksheet.Cell(totalRow, 5).Style.Font.Bold = true;

        // 枠線
        var dataRange = worksheet.Range(1, 1, totalRow, headers.Length);
        dataRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        dataRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        dataRange.Style.Border.OutsideBorderColor = XLColor.Black;
        dataRange.Style.Border.InsideBorderColor = XLColor.Gray;

        // 数値列を中央揃え
        var numRange = worksheet.Range(2, 2, totalRow, headers.Length);
        numRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        // 列幅
        worksheet.Column(1).Width = 30;
        worksheet.Column(2).Width = 12;
        worksheet.Column(3).Width = 12;
        worksheet.Column(4).Width = 12;
        worksheet.Column(5).Width = 12;

        worksheet.SheetView.FreezeRows(1);

        Console.WriteLine("政党別回答状況シートを追加しました。");
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
            "無回答",
            "Q2理由"
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

            var noAnswerCol = headers.Count - 1; // 「無回答」列
            var reasonCol = headers.Count;       // 「Q2理由」列

            // 無回答チェック
            var isNoAnswer = item.Q2Reason == "無回答";

            if (isNoAnswer)
            {
                // 無回答の場合は無回答列に○
                worksheet.Cell(row, noAnswerCol).Value = "○";
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

            // Q2理由を出力
            worksheet.Cell(row, reasonCol).Value = item.Q2Reason ?? string.Empty;
        }

        // 枠線設定
        var dataRange = worksheet.Range(1, 1, data.Count + 1, headers.Count);
        dataRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        dataRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        dataRange.Style.Border.OutsideBorderColor = XLColor.Black;
        dataRange.Style.Border.InsideBorderColor = XLColor.Gray;

        // 選択肢列を中央揃え（Q2理由列は除く）
        var choiceRange = worksheet.Range(2, 4, data.Count + 1, headers.Count - 1);
        choiceRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        // 列幅調整
        worksheet.Column(1).Width = 15;  // 候補者名
        worksheet.Column(2).Width = 20;  // 選挙区
        worksheet.Column(3).Width = 15;  // 政党
        for (var i = 4; i <= headers.Count - 1; i++)
        {
            worksheet.Column(i).Width = 8;  // 選択肢列
        }
        worksheet.Column(headers.Count).Width = 40;  // Q2理由

        // ヘッダー行を固定
        worksheet.SheetView.FreezeRows(1);

        // オートフィルター設定
        worksheet.RangeUsed()?.SetAutoFilter();

        Console.WriteLine("Q2フィルターシートを追加しました。");
    }

    /// <summary>
    /// Q1政党別集計データシートを追加
    /// </summary>
    private static void AddQ1ChartDataSheet(IXLWorkbook workbook, List<Shuinsen2026Respond> data)
    {
        var worksheet = workbook.Worksheets.Add("Q1政党別集計");

        // Q1選択肢ラベル
        var q1Labels = new[]
        {
            "A.法律で禁止するべき",
            "B.法律で禁止するべきではない",
            "C.どちらともいえない、答えない"
        };
        var q1Keys = Q1ChoiceOrder.Keys.ToArray(); // 選択肢の元テキスト

        // 政党別にグループ化してQ1回答を集計
        var partyGroups = data
            .GroupBy(d => NormalizePartyName(d.PartyName))
            .OrderBy(g => GetPartySortKey(g.Key))
            .Select(g => new
            {
                PartyName = g.Key,
                Counts = q1Keys.Select(key => g.Count(d => d.Q1Answer.Trim() == key)).ToArray()
            })
            .ToList();

        // 割合列のラベル
        var q1PctLabels = new[]
        {
            "A.割合(%)",
            "B.割合(%)",
            "C.割合(%)"
        };
        var totalColCount = 1 + q1Labels.Length + q1PctLabels.Length; // 政党名 + 数値3列 + 割合3列

        // ヘッダー
        worksheet.Cell(1, 1).Value = "政党名";
        for (var i = 0; i < q1Labels.Length; i++)
        {
            worksheet.Cell(1, i + 2).Value = q1Labels[i];
        }
        for (var i = 0; i < q1PctLabels.Length; i++)
        {
            worksheet.Cell(1, q1Labels.Length + 2 + i).Value = q1PctLabels[i];
        }

        // ヘッダースタイル
        var headerRange = worksheet.Range(1, 1, 1, totalColCount);
        headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#4472C4");
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Font.FontColor = XLColor.White;
        headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        headerRange.Style.Alignment.WrapText = true;

        // データ行
        for (var rowIndex = 0; rowIndex < partyGroups.Count; rowIndex++)
        {
            var party = partyGroups[rowIndex];
            var row = rowIndex + 2;
            worksheet.Cell(row, 1).Value = party.PartyName;
            for (var c = 0; c < party.Counts.Length; c++)
            {
                worksheet.Cell(row, c + 2).Value = party.Counts[c];
            }
            // 割合列: 各回答数 / (B+C+D) の行合計
            var sumColLetters = string.Join("+", Enumerable.Range(0, q1Labels.Length).Select(c => $"{(char)('B' + c)}{row}"));
            for (var c = 0; c < q1PctLabels.Length; c++)
            {
                var valCol = (char)('B' + c);
                var pctCol = q1Labels.Length + 2 + c;
                worksheet.Cell(row, pctCol).FormulaA1 = $"IF(({sumColLetters})=0,0,{valCol}{row}/({sumColLetters}))";
                worksheet.Cell(row, pctCol).Style.NumberFormat.Format = "0.0%";
            }
        }

        // 合計行を追加（円グラフ用）
        var totalRow = partyGroups.Count + 2;
        worksheet.Cell(totalRow, 1).Value = "合計";
        worksheet.Cell(totalRow, 1).Style.Font.Bold = true;
        for (var c = 0; c < q1Labels.Length; c++)
        {
            var colLetter = (char)('B' + c);
            worksheet.Cell(totalRow, c + 2).FormulaA1 = $"SUM({colLetter}2:{colLetter}{partyGroups.Count + 1})";
            worksheet.Cell(totalRow, c + 2).Style.Font.Bold = true;
        }
        // 合計行の割合
        var totalSumLetters = string.Join("+", Enumerable.Range(0, q1Labels.Length).Select(c => $"{(char)('B' + c)}{totalRow}"));
        for (var c = 0; c < q1PctLabels.Length; c++)
        {
            var valCol = (char)('B' + c);
            var pctCol = q1Labels.Length + 2 + c;
            worksheet.Cell(totalRow, pctCol).FormulaA1 = $"IF(({totalSumLetters})=0,0,{valCol}{totalRow}/({totalSumLetters}))";
            worksheet.Cell(totalRow, pctCol).Style.NumberFormat.Format = "0.0%";
            worksheet.Cell(totalRow, pctCol).Style.Font.Bold = true;
        }

        // 枠線
        var dataRange = worksheet.Range(1, 1, totalRow, totalColCount);
        dataRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        dataRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        dataRange.Style.Border.OutsideBorderColor = XLColor.Black;
        dataRange.Style.Border.InsideBorderColor = XLColor.Gray;

        // 数値列・割合列を中央揃え
        var numRange = worksheet.Range(2, 2, totalRow, totalColCount);
        numRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        // 列幅
        worksheet.Column(1).Width = 30; // 政党名
        for (var i = 2; i <= q1Labels.Length + 1; i++)
        {
            worksheet.Column(i).Width = 20;
        }
        for (var i = q1Labels.Length + 2; i <= totalColCount; i++)
        {
            worksheet.Column(i).Width = 12; // 割合列
        }

        worksheet.SheetView.FreezeRows(1);

        Console.WriteLine("Q1政党別集計シートを追加しました。");
    }

    /// <summary>
    /// Q2政党別集計データシートを追加
    /// </summary>
    private static void AddQ2ChartDataSheet(IXLWorkbook workbook, List<Shuinsen2026Respond> data)
    {
        var worksheet = workbook.Worksheets.Add("Q2政党別集計");

        // Q2選択肢ラベル（A〜I + 無回答）
        var q2Labels = new[]
        {
            "A.刑法のわいせつ物頒布等罪による規制",
            "B.AV新法による規制",
            "C.クレジットカード決済の制約",
            "D.ジェンダー平等論や多様性配慮に基づく表現制限",
            "E.新サイバー犯罪条約による創作物への規制",
            "F.「不適切」を理由とする広告の法的規制",
            "G.AI・機械学習に対する法的規制",
            "H.国旗損壊罪による創作表現規制の懸念",
            "I.特にない",
            "無回答"
        };
        var q2Keys = Q2ChoiceOrder.Keys.ToArray(); // 選択肢の元テキスト（9個）
        var choiceCount = q2Labels.Length; // 10（9選択肢+無回答）

        // 政党別にグループ化してQ2回答を集計
        var partyGroups = data
            .GroupBy(d => NormalizePartyName(d.PartyName))
            .OrderBy(g => GetPartySortKey(g.Key))
            .Select(g =>
            {
                var counts = new int[choiceCount];
                foreach (var d in g)
                {
                    var isNoAnswer = d.Q2Reason == "無回答";
                    if (isNoAnswer)
                    {
                        counts[choiceCount - 1]++; // 無回答列
                    }
                    else
                    {
                        var selectedChoices = d.Q2Answer
                            .Split(',', StringSplitOptions.RemoveEmptyEntries)
                            .Select(a => a.Trim())
                            .ToHashSet();
                        for (var i = 0; i < q2Keys.Length; i++)
                        {
                            if (selectedChoices.Contains(q2Keys[i]))
                            {
                                counts[i]++;
                            }
                        }
                    }
                }
                return new { PartyName = g.Key, Counts = counts, Total = g.Count() };
            })
            .ToList();

        // 割合列のラベル
        var q2PctLabels = new[]
        {
            "A.割合(%)", "B.割合(%)", "C.割合(%)", "D.割合(%)", "E.割合(%)",
            "F.割合(%)", "G.割合(%)", "H.割合(%)", "I.割合(%)", "無回答割合(%)"
        };
        var totalColCount = 1 + choiceCount + choiceCount; // 政党名 + 数値10列 + 割合10列 = 21

        // ヘッダー
        worksheet.Cell(1, 1).Value = "政党名";
        for (var i = 0; i < choiceCount; i++)
        {
            worksheet.Cell(1, i + 2).Value = q2Labels[i];
        }
        for (var i = 0; i < choiceCount; i++)
        {
            worksheet.Cell(1, choiceCount + 2 + i).Value = q2PctLabels[i];
        }

        // ヘッダースタイル
        var headerRange = worksheet.Range(1, 1, 1, totalColCount);
        headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#4472C4");
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Font.FontColor = XLColor.White;
        headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        headerRange.Style.Alignment.WrapText = true;

        // データ行
        for (var rowIndex = 0; rowIndex < partyGroups.Count; rowIndex++)
        {
            var party = partyGroups[rowIndex];
            var row = rowIndex + 2;
            worksheet.Cell(row, 1).Value = party.PartyName;
            for (var c = 0; c < choiceCount; c++)
            {
                worksheet.Cell(row, c + 2).Value = party.Counts[c];
            }
            // 割合列: 各選択肢の人数 / その政党の回答者総数
            for (var c = 0; c < choiceCount; c++)
            {
                var pctCol = choiceCount + 2 + c;
                var total = party.Total;
                worksheet.Cell(row, pctCol).Value = total == 0 ? 0 : (double)party.Counts[c] / total;
                worksheet.Cell(row, pctCol).Style.NumberFormat.Format = "0.0%";
            }
        }

        // 合計行
        var totalRow = partyGroups.Count + 2;
        worksheet.Cell(totalRow, 1).Value = "合計";
        worksheet.Cell(totalRow, 1).Style.Font.Bold = true;
        var grandTotal = data.Count;
        var grandCounts = new int[choiceCount];
        foreach (var party in partyGroups)
        {
            for (var c = 0; c < choiceCount; c++)
            {
                grandCounts[c] += party.Counts[c];
            }
        }
        for (var c = 0; c < choiceCount; c++)
        {
            worksheet.Cell(totalRow, c + 2).Value = grandCounts[c];
            worksheet.Cell(totalRow, c + 2).Style.Font.Bold = true;

            var pctCol = choiceCount + 2 + c;
            worksheet.Cell(totalRow, pctCol).Value = grandTotal == 0 ? 0 : (double)grandCounts[c] / grandTotal;
            worksheet.Cell(totalRow, pctCol).Style.NumberFormat.Format = "0.0%";
            worksheet.Cell(totalRow, pctCol).Style.Font.Bold = true;
        }

        // 枠線
        var dataRange = worksheet.Range(1, 1, totalRow, totalColCount);
        dataRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        dataRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        dataRange.Style.Border.OutsideBorderColor = XLColor.Black;
        dataRange.Style.Border.InsideBorderColor = XLColor.Gray;

        // 数値列・割合列を中央揃え
        var numRange = worksheet.Range(2, 2, totalRow, totalColCount);
        numRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        // 列幅
        worksheet.Column(1).Width = 30; // 政党名
        for (var i = 2; i <= choiceCount + 1; i++)
        {
            worksheet.Column(i).Width = 12; // 数値列
        }
        for (var i = choiceCount + 2; i <= totalColCount; i++)
        {
            worksheet.Column(i).Width = 12; // 割合列
        }

        worksheet.SheetView.FreezeRows(1);

        Console.WriteLine("Q2政党別集計シートを追加しました。");
    }

    /// <summary>
    /// 保存済みExcelファイルにEPPlusでチャートを追加する
    /// </summary>
    public static void AddChartsToExcel(string filePath)
    {
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

        using var package = new ExcelPackage(new FileInfo(filePath));
        var ws = package.Workbook.Worksheets["Q1政党別集計"];
        if (ws == null)
        {
            Console.WriteLine("Q1政党別集計シートが見つかりません。チャート追加をスキップします。");
            return;
        }

        // データ範囲を特定（ヘッダー行=1, データ行=2～N, 合計行=N+1）
        var lastRow = ws.Dimension.End.Row;        // 合計行含む最終行
        var lastDataRow = lastRow - 1;             // 合計行の手前
        var dataRowCount = lastDataRow - 1;        // ヘッダー除くデータ行数
        if (dataRowCount <= 0)
        {
            Console.WriteLine("データがないためチャート追加をスキップします。");
            return;
        }

        var catAddress = $"A2:A{lastDataRow}";     // 政党名（カテゴリ軸）
        var seriesColors = new[] { "#E04040", "#4472C4", "#A5A5A5" };
        var seriesCols = new[] { "B", "C", "D" };

        // --- チャート1: 集合縦棒グラフ ---
        var chartRow = lastRow + 3;
        var clustered = ws.Drawings.AddChart("ClusteredBar", eChartType.ColumnClustered);
        clustered.SetPosition(chartRow - 1, 0, 0, 0);
        clustered.SetSize(700, 400);
        clustered.Title.Text = "Q1 政党別回答（集合縦棒）";
        clustered.Legend.Position = eLegendPosition.Bottom;
        for (var i = 0; i < 3; i++)
        {
            var valRange = ws.Cells[$"{seriesCols[i]}2:{seriesCols[i]}{lastDataRow}"];
            var catRange = ws.Cells[catAddress];
            var s = clustered.Series.Add(valRange, catRange);
            s.HeaderAddress = ws.Cells[$"{seriesCols[i]}1"];
            s.Fill.Color = System.Drawing.ColorTranslator.FromHtml(seriesColors[i]);
        }

        // --- チャート2: 積み上げ棒グラフ ---
        var stacked = ws.Drawings.AddChart("StackedBar", eChartType.BarStacked100);
        stacked.SetPosition(chartRow - 1, 0, 10, 0);
        stacked.SetSize(700, 400);
        stacked.Title.Text = "Q1 政党別回答（積み上げ）";
        stacked.Legend.Position = eLegendPosition.Bottom;
        for (var i = 0; i < 3; i++)
        {
            var valRange = ws.Cells[$"{seriesCols[i]}2:{seriesCols[i]}{lastDataRow}"];
            var catRange = ws.Cells[catAddress];
            var s = stacked.Series.Add(valRange, catRange);
            s.HeaderAddress = ws.Cells[$"{seriesCols[i]}1"];
            s.Fill.Color = System.Drawing.ColorTranslator.FromHtml(seriesColors[i]);
        }
        if (stacked is ExcelBarChart stackedBarChart)
        {
            stackedBarChart.DataLabel.ShowValue = true;
            stackedBarChart.DataLabel.ShowPercent = false;
            stackedBarChart.DataLabel.ShowCategory = false;
            stackedBarChart.DataLabel.ShowSeriesName = false;
        }

        // --- チャート3: 全体円グラフ ---
        var pieRow = chartRow + 22;
        var overallPie = ws.Drawings.AddChart("OverallPie", eChartType.Pie);
        overallPie.SetPosition(pieRow - 1, 0, 0, 0);
        overallPie.SetSize(700, 400);
        overallPie.Title.Text = "Q1 全体回答割合";
        overallPie.Legend.Position = eLegendPosition.Bottom;
        var totalValRange = ws.Cells[$"B{lastRow}:D{lastRow}"];   // 合計行
        var totalCatRange = ws.Cells["B1:D1"];                     // ヘッダー（選択肢名）
        var pieSeries = overallPie.Series.Add(totalValRange, totalCatRange);
        pieSeries.HeaderAddress = ws.Cells[$"A{lastRow}"];
        if (overallPie is ExcelPieChart overallPieChart)
        {
            overallPieChart.DataLabel.ShowValue = true;
            overallPieChart.DataLabel.ShowPercent = true;
            overallPieChart.DataLabel.ShowCategory = false;
            for (var i = 0; i < 3; i++)
            {
                overallPieChart.Series[0].DataPoints.Add(i).Fill.Color = System.Drawing.ColorTranslator.FromHtml(seriesColors[i]);
            }
        }

        // --- チャート4: 政党別円グラフ群 ---
        var chartsPerRow = 4;
        var chartWidth = 350;
        var chartHeight = 300;

        for (var i = 0; i < dataRowCount; i++)
        {
            var gridRow = i / chartsPerRow;
            var gridCol = i % chartsPerRow;
            var dataRow = i + 2;
            var partyName = ws.Cells[$"A{dataRow}"].Text;

            var chartName = $"PartyPie_{i}";
            var partyPie = ws.Drawings.AddChart(chartName, eChartType.Pie);
            partyPie.SetPosition(pieRow + 22 + gridRow * 18 - 1, 0, 10 + gridCol * 5, 0);
            partyPie.SetSize(chartWidth, chartHeight);
            partyPie.Title.Text = partyName;
            partyPie.Title.Font.Size = 10;
            partyPie.Legend.Position = eLegendPosition.Bottom;

            var partyValRange = ws.Cells[$"B{dataRow}:D{dataRow}"];
            var partyCatRange = ws.Cells["B1:D1"];
            var ps = partyPie.Series.Add(partyValRange, partyCatRange);
            ps.HeaderAddress = ws.Cells[$"A{dataRow}"];
            if (partyPie is ExcelPieChart partyPieChart)
            {
                partyPieChart.DataLabel.ShowValue = true;
                partyPieChart.DataLabel.ShowPercent = true;
                partyPieChart.DataLabel.ShowCategory = false;
                for (var j = 0; j < 3; j++)
                {
                    partyPieChart.Series[0].DataPoints.Add(j).Fill.Color = System.Drawing.ColorTranslator.FromHtml(seriesColors[j]);
                }
            }
        }

        Console.WriteLine("Q1政党別集計シートにチャートを追加しました。");

        // --- Q2政党別集計シートにチャートを追加 ---
        AddQ2ChartsToWorksheet(package);

        // --- 政党別回答状況シートにチャートを追加 ---
        AddPartyResponseChartsToWorksheet(package);

        package.Save();
    }

    /// <summary>
    /// Q2政党別集計シートにチャートを追加する（EPPlusパッケージ内で呼び出し）
    /// </summary>
    private static void AddQ2ChartsToWorksheet(ExcelPackage package)
    {
        var ws = package.Workbook.Worksheets["Q2政党別集計"];
        if (ws == null)
        {
            Console.WriteLine("Q2政党別集計シートが見つかりません。チャート追加をスキップします。");
            return;
        }

        var lastRow = ws.Dimension.End.Row;        // 合計行含む最終行
        var lastDataRow = lastRow - 1;             // 合計行の手前
        var dataRowCount = lastDataRow - 1;        // ヘッダー除くデータ行数
        if (dataRowCount <= 0)
        {
            Console.WriteLine("Q2データがないためチャート追加をスキップします。");
            return;
        }

        // Q2は10列（B〜K: 9選択肢+無回答）
        var seriesCols = new[] { "B", "C", "D", "E", "F", "G", "H", "I", "J", "K" };
        var catAddress = $"A2:A{lastDataRow}"; // 政党名（カテゴリ軸）

        // --- チャート1: 全体集合横棒グラフ（合計行の選択肢別人数） ---
        var chartRow = lastRow + 3;
        var overallBar = ws.Drawings.AddChart("Q2OverallBar", eChartType.BarClustered);
        overallBar.SetPosition(chartRow - 1, 0, 0, 0);
        overallBar.SetSize(900, 500);
        overallBar.Title.Text = "Q2 全体回答（選択肢別人数）";
        overallBar.Legend.Position = eLegendPosition.Bottom;

        // 合計行のB〜Kをデータ、ヘッダー行のB1〜K1をカテゴリとして1系列の横棒グラフ
        var totalValRange = ws.Cells[$"B{lastRow}:K{lastRow}"];
        var totalCatRange = ws.Cells["B1:K1"];
        var overallSeries = overallBar.Series.Add(totalValRange, totalCatRange);
        overallSeries.HeaderAddress = ws.Cells[$"A{lastRow}"];
        if (overallBar is ExcelBarChart overallBarChart)
        {
            overallBarChart.DataLabel.ShowValue = true;
            overallBarChart.DataLabel.ShowCategory = false;
            overallBarChart.DataLabel.ShowSeriesName = false;
        }

        // --- チャート2: 政党別の個別横棒グラフ ---
        var chartsPerRow = 2;
        var chartWidth = 700;
        var chartHeight = 400;
        var baseRow = chartRow + 28;

        for (var i = 0; i < dataRowCount; i++)
        {
            var gridRow = i / chartsPerRow;
            var gridCol = i % chartsPerRow;
            var dataRow = i + 2;
            var partyName = ws.Cells[$"A{dataRow}"].Text;

            var chartName = $"Q2PartyBar_{i}";
            var partyBar = ws.Drawings.AddChart(chartName, eChartType.BarClustered);
            partyBar.SetPosition(baseRow + gridRow * 22 - 1, 0, gridCol * 10, 0);
            partyBar.SetSize(chartWidth, chartHeight);
            partyBar.Title.Text = $"Q2 {partyName}";
            partyBar.Title.Font.Size = 11;
            partyBar.Legend.Position = eLegendPosition.Bottom;

            // 該当政党行のB〜Kをデータ、ヘッダー行をカテゴリ
            var partyValRange = ws.Cells[$"B{dataRow}:K{dataRow}"];
            var partyCatRange = ws.Cells["B1:K1"];
            var s = partyBar.Series.Add(partyValRange, partyCatRange);
            s.HeaderAddress = ws.Cells[$"A{dataRow}"];
            if (partyBar is ExcelBarChart partyBarChart)
            {
                partyBarChart.DataLabel.ShowValue = true;
                partyBarChart.DataLabel.ShowCategory = false;
                partyBarChart.DataLabel.ShowSeriesName = false;
            }
        }

        Console.WriteLine("Q2政党別集計シートにチャートを追加しました。");
    }

    /// <summary>
    /// 政党別回答状況シートに円グラフを追加する（EPPlusパッケージ内で呼び出し）
    /// </summary>
    private static void AddPartyResponseChartsToWorksheet(ExcelPackage package)
    {
        var ws = package.Workbook.Worksheets["政党別回答状況"];
        if (ws == null)
        {
            Console.WriteLine("政党別回答状況シートが見つかりません。チャート追加をスキップします。");
            return;
        }

        var lastRow = ws.Dimension.End.Row;    // 合計行含む最終行
        var lastDataRow = lastRow - 1;         // 合計行の手前
        var dataRowCount = lastDataRow - 1;    // ヘッダー除くデータ行数
        if (dataRowCount <= 0)
        {
            Console.WriteLine("政党別回答状況データがないためチャート追加をスキップします。");
            return;
        }

        // 円グラフの色: 回答=青, 未回答=グレー
        var responseColor = System.Drawing.ColorTranslator.FromHtml("#4472C4");
        var noResponseColor = System.Drawing.ColorTranslator.FromHtml("#A5A5A5");

        // 政党別円グラフ（回答数C列 vs 未回答数D列）
        var chartsPerRow = 4;
        var chartWidth = 350;
        var chartHeight = 300;
        var baseRow = lastRow + 3;

        for (var i = 0; i < dataRowCount; i++)
        {
            var gridRow = i / chartsPerRow;
            var gridCol = i % chartsPerRow;
            var dataRow = i + 2;
            var partyName = ws.Cells[$"A{dataRow}"].Text;

            var chartName = $"ResponsePie_{i}";
            var pie = ws.Drawings.AddChart(chartName, eChartType.Pie);
            pie.SetPosition(baseRow + gridRow * 18 - 1, 0, gridCol * 5, 0);
            pie.SetSize(chartWidth, chartHeight);
            pie.Title.Text = partyName;
            pie.Title.Font.Size = 10;
            pie.Legend.Position = eLegendPosition.Bottom;

            // C列(回答数)とD列(未回答数)を値、ヘッダーをカテゴリ
            var valRange = ws.Cells[$"C{dataRow}:D{dataRow}"];
            var catRange = ws.Cells["C1:D1"];
            var s = pie.Series.Add(valRange, catRange);
            s.HeaderAddress = ws.Cells[$"A{dataRow}"];
            if (pie is ExcelPieChart pieChart)
            {
                pieChart.DataLabel.ShowValue = true;
                pieChart.DataLabel.ShowPercent = true;
                pieChart.DataLabel.ShowCategory = false;
                pieChart.Series[0].DataPoints.Add(0).Fill.Color = responseColor;
                pieChart.Series[0].DataPoints.Add(1).Fill.Color = noResponseColor;
            }
        }

        Console.WriteLine("政党別回答状況シートに円グラフを追加しました。");
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

            比例ブロック出力内容Dic.Add(blk, respondList.OrderBy(r => GetPartySortKey(r.PartyName)).ThenBy(r => r.NameFurigana).ToList());
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