using ElectionPageCreateTool;
using ElectionPageCreateTool.Logic;


var (json, directoryPath) = TsvFileConvert.ConvertToJson();

if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(directoryPath))
{
    Console.WriteLine("JSONの生成に失敗しました。プログラムを終了します。");
    return;
}

// Shuinsen2024Logic.GenerateShuinsen2024(json, directoryPath);
// Togisen2025Logic.Generate(json, directoryPath);
Saninsen2025Logic.GenerateSaninsen2025(json, directoryPath);



Console.WriteLine("出力が完了しました。何か入力するとプログラムが終了します。");
Console.ReadKey();