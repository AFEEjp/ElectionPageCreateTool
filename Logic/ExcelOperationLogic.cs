using ClosedXML.Excel;

namespace ElectionPageCreateTool.Logic
{
    public class ExcelOperationLogic
    {
        /// <summary>
        /// Excelファイルを読み込み、Dictionaryに変換
        /// </summary>
        /// <param name="filePath">Excelファイルのパス</param>
        /// <returns>JSON文字列</returns>
        public List<Dictionary<string, string>> ConvertExcelToDictionary(string filePath)
        {
            try
            {
                using var workbook = new XLWorkbook(filePath);

                // 1シート目を取得
                var worksheet = workbook.Worksheet(1);
                var headers = GetHeaders(worksheet);

                // データを格納するリスト
                var dataList = new List<Dictionary<string, string>>();

                // 2行目から最終行までループ
                var rowIndex = 2;
                while (!string.IsNullOrEmpty(worksheet.Cell(rowIndex, 1).Value.ToString()))
                {
                    var dataRow = new Dictionary<string, string>();

                    // 各列のデータを取得
                    for (var colIndex = 1; colIndex <= headers.Count; colIndex++)
                    {
                        var cellValue = worksheet.Cell(rowIndex, colIndex).Value.ToString();
                        dataRow.Add(headers[colIndex - 1], cellValue);
                    }

                    dataList.Add(dataRow);
                    rowIndex++;
                }

                return dataList;
            }
            catch (Exception ex)
            {
                throw new Exception($"Excelファイルの処理中にエラーが発生しました: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 選挙結果Excelファイルをマージする機能
        /// </summary>
        /// <param name="mainFilePath"></param>
        /// <param name="resultFilePath"></param>
        /// <param name="outputPath"></param>
        public void MergeElectionResultExcel(string mainFilePath, string resultFilePath, string? outputPath = null)
        {
            Console.WriteLine($"メインファイル: {mainFilePath} と結果ファイル: {resultFilePath} を処理しています...");

            // 出力パスが指定されていない場合、元のファイル名に "_merged" を追加
            if (string.IsNullOrEmpty(outputPath))
            {
                var directory = Path.GetDirectoryName(mainFilePath);
                var fileName = Path.GetFileNameWithoutExtension(mainFilePath) + "_merged" + Path.GetExtension(mainFilePath);
                outputPath = Path.Combine(directory, fileName);
            }

            // メインのExcelファイルを開く（アンケート結果）
            using var mainWorkbook = new XLWorkbook(mainFilePath);
            var mainWorksheet = mainWorkbook.Worksheets.First();

            // 結果のExcelファイルを開く（当落情報）
            using var resultWorkbook = new XLWorkbook(resultFilePath);
            var resultWorksheet = resultWorkbook.Worksheets.First();

            // ヘッダー行を取得
            var mainHeaders = GetHeaders(mainWorksheet);
            var resultHeaders = GetHeaders(resultWorksheet);

            // 当落情報のインデックスを取得
            var lastNameIndex = resultHeaders.IndexOf("立候補者姓");
            var firstNameIndex = resultHeaders.IndexOf("立候補者名");
            var resultIndex = resultHeaders.IndexOf("当落");

            if (lastNameIndex == -1 || firstNameIndex == -1 || resultIndex == -1)
            {
                Console.WriteLine("結果ファイルに必要なヘッダーが見つかりませんでした。");
                return;
            }

            // 結果データを読み込んでディクショナリに格納（キー：フルネーム、値：当落結果）
            var resultData = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var resultRowIndex = 2; // ヘッダー行の次から

            while (!string.IsNullOrEmpty(resultWorksheet.Cell(resultRowIndex, 1).Value.ToString()))
            {
                var lastName = resultWorksheet.Cell(resultRowIndex, lastNameIndex + 1).Value.ToString().Trim();
                var firstName = resultWorksheet.Cell(resultRowIndex, firstNameIndex + 1).Value.ToString().Trim();
                var result = resultWorksheet.Cell(resultRowIndex, resultIndex + 1).Value.ToString().Trim();

                // 姓名を連結してキーを作成（空白を除去）
                var fullName = (lastName + firstName).Replace(" ", "").Replace("　", "");
                resultData[fullName] = result;

                resultRowIndex++;
            }

            // メインファイルに当落列を追加
            var lastColumnIndex = mainHeaders.Count + 1;
            mainWorksheet.Cell(1, lastColumnIndex).Value = "当落";

            // メインファイルの各行に当落情報を追加
            var mainRowIndex = 2; // ヘッダー行の次から
            var candidateNameIndex = mainHeaders.IndexOf("候補者名");

            if (candidateNameIndex == -1)
            {
                Console.WriteLine("メインファイルに「候補者名」ヘッダーが見つかりませんでした。");
                return;
            }

            var matchCount = 0;
            var totalCount = 0;

            while (!string.IsNullOrEmpty(mainWorksheet.Cell(mainRowIndex, 1).Value.ToString()))
            {
                var candidateName = mainWorksheet.Cell(mainRowIndex, candidateNameIndex + 1).Value.ToString().Trim();
                // 空白を除去
                var normalizedName = candidateName.Replace(" ", "").Replace("　", "");
                totalCount++;

                if (resultData.TryGetValue(normalizedName, out var result))
                {
                    mainWorksheet.Cell(mainRowIndex, lastColumnIndex).Value = result;
                    matchCount++;
                }
                else
                {
                    mainWorksheet.Cell(mainRowIndex, lastColumnIndex).Value = "不明";
                }

                mainRowIndex++;
            }

            // 変更を保存
            mainWorkbook.SaveAs(outputPath);

            Console.WriteLine($"処理が完了しました。合計 {totalCount} 件中 {matchCount} 件の候補者の当落情報を追加しました。");
            Console.WriteLine($"結果は {outputPath} に保存されました。");
        }



        /// <summary>
        /// ヘッダー行を取得するヘルパーメソッド
        /// </summary>
        /// <param name="worksheet"></param>
        /// <returns></returns>
        private List<string> GetHeaders(IXLWorksheet worksheet)
        {
            var headers = new List<string>();
            var colIndex = 1;

            while (!string.IsNullOrEmpty(worksheet.Cell(1, colIndex).Value.ToString()))
            {
                headers.Add(worksheet.Cell(1, colIndex).Value.ToString());
                colIndex++;
            }

            return headers;
        }
    }
}
