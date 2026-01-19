using ElectionPageCreateTool.Logic.ElectionLogic;



Console.WriteLine("どの選挙のロジックを実行しますか？");
Console.WriteLine("1. 2024衆院選");
Console.WriteLine("2. 2025都議選");
Console.WriteLine("3. 2025参院選");
Console.WriteLine("4. 2026衆院選");
var electionChoice = Console.ReadLine();

switch (electionChoice)
{
    case "1":
        Shuinsen2024Logic.MainLogic();

        break;
    case "2":
        Togisen2025Logic.MainLogic();

        break;
    case "3":
        Saninsen2025Logic.MainLogic();

        break;
    case "4":
        Shuinsen2026Logic.MainLogic();

        break;
    default:
        Console.WriteLine("無効な選択です。プログラムを終了します。");
        return;
}

Console.WriteLine("何か入力するとプログラムが終了します。");
Console.ReadKey();
