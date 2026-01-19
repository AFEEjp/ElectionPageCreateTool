using ElectionPageCreateTool;
using ElectionPageCreateTool.Logic;
using ElectionPageCreateTool.Logic.ElectionLogic;

void ExecutePastElection()
{
    Console.Clear();
    Console.WriteLine("過去の選挙用ロジックを実行します。");
    var (json, directoryPath) = TsvFileConvert.ConvertToJson();

    if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(directoryPath))
    {
        Console.WriteLine("JSONの生成に失敗しました。プログラムを終了します。");
        return;
    }

    Console.WriteLine("どの選挙のロジックを実行しますか？");
    Console.WriteLine("1. 2024衆院選");
    Console.WriteLine("2. 2025都議選");
    Console.WriteLine("3. 2025参院選");
    var electionChoice = Console.ReadLine();

    switch (electionChoice)
    {
        case "1":
            Shuinsen2024Logic.GenerateShuinsen2024(json, directoryPath);
            break;
        case "2":
            Togisen2025Logic.Generate(json, directoryPath);
            break;
        case "3":
            Saninsen2025Logic.GenerateSaninsen2025(json, directoryPath);
            break;
        default:
            Console.WriteLine("無効な選択です。プログラムを終了します。");
            return;
    }
}

static string NormalizeExplorerCopiedPath(string? input)
{
    if (string.IsNullOrWhiteSpace(input))
        return string.Empty;

    var s = input.Trim();

    // 先頭末尾が " で囲まれている場合のみ外す
    if (s is ['"', _, ..] && s[^1] == '"')
        s = s[1..^1];

    return s;
}

var excelOperationLogic = new ExcelOperationLogic();

Console.WriteLine("処理を選択してください:");
Console.WriteLine("1. ExcelファイルをSQLiteに保存");
Console.WriteLine("2. 選挙結果Excelファイルをマージ");
Console.WriteLine("9. 過去の選挙用ロジックを呼び出す");

var choice = Console.ReadLine();

switch (choice)
{
    case "1":
        Console.WriteLine("Excelファイルのパスを入力してください:");
        var excelPath = Console.ReadLine();
        if (!string.IsNullOrEmpty(excelPath))
        {
            var dic = excelOperationLogic.ConvertExcelToDictionary(NormalizeExplorerCopiedPath(excelPath));

            Console.WriteLine($"Excelから {dic.Count} 件のデータを読み込みました。");

            var dbLogic = new DatabaseOperationLogic();
            Console.WriteLine($"データベースファイル: {dbLogic.GetDbFilePath()}");
            Console.WriteLine($"テーブル名: {dbLogic.GetTableName()}");
            dbLogic.ShowUpsertKeyColumns();

            dbLogic.UpsertDataToSQLite(dbLogic.GetTableName(), dic);
        }
        break;

    case "2":
        Console.WriteLine("メインExcelファイル（アンケート結果）のパスを入力してください:");
        var mainFilePath = Console.ReadLine();
        Console.WriteLine("結果Excelファイル（当落情報）のパスを入力してください:");
        var resultFilePath = Console.ReadLine();
        Console.WriteLine("出力ファイルパスを入力してください（空白の場合は自動生成）:");
        var outputPath = Console.ReadLine();

        if (!string.IsNullOrEmpty(mainFilePath) && !string.IsNullOrEmpty(resultFilePath))
        {
            excelOperationLogic.MergeElectionResultExcel(mainFilePath, resultFilePath, outputPath);
        }

        break;

    case "9":
        ExecutePastElection();

        break;
}

Console.WriteLine("何か入力するとプログラムが終了します。");
Console.ReadKey();


