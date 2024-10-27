using System.Text;
using System.Text.Json;
using ElectionPageCreateTool.Extensions;
using ElectionPageCreateTool.Model;

string? filePath;
while (true)
{
    Console.Write("TSVファイルのパスを入力してください: ");
    filePath = Console.ReadLine();

    if (File.Exists(filePath))
    {
        break; // ファイルが存在する場合、ループを終了
    }
    else
    {
        Console.WriteLine("ファイルが存在しません。再度入力してください。");
    }
}

//指定されたパスからファイルの存在位置を取得
var directoryPath = Path.GetDirectoryName(filePath);
if (!Directory.Exists(directoryPath))
{
    Console.WriteLine("ディレクトリが存在しません。");
    return;
}

string json;
{
    var lines = File.ReadAllLines(filePath);
    var headers = lines[0].Split('\t');
    var jsonList = new List<Dictionary<string, string>>();

    for (var i = 1; i < lines.Length; i++)
    {
        var values = lines[i].Split('\t');
        var jsonEntry = new Dictionary<string, string>();

        for (var j = 0; j < headers.Length; j++)
        {
            jsonEntry[headers[j]] = values.Length > j ? values[j] : string.Empty;
        }

        jsonList.Add(jsonEntry);
    }

    json = JsonSerializer.Serialize(jsonList);
}

File.WriteAllText(Path.Combine(directoryPath, $"{DateTime.Now:yyyyMMddhhmmss}.json"), json);

// JSONをクラスのリストにパース
var list = JsonSerializer.Deserialize<List<Shuinsen2024Respond>>(json);

if (list == null)
{
    Console.WriteLine("パース失敗？");
    return;
}

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
var 小選挙区出力内容Dic = new List<(Prefecture 都道府県名, Dictionary<(string EnStr, string JpStr), List<Shuinsen2024Respond>> 候補者情報リスト)>();
foreach (var kv in 小選挙区Dic)
{
    var kuwariList = kv.Value;

    var 区分けDic = new Dictionary<(string EnStr, string JpStr), List<Shuinsen2024Respond>>();
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
    strBldr.AppendLine($"<h3 id='pref-{vt.都道府県名.ToString().ToLower()}'>{vt.都道府県名.GetDescription()}</h3>");
    strBldr.AppendLine("<div>");

    foreach (var kv in vt.候補者情報リスト)
    {
        strBldr.AppendLine($"<h4 id='kuwari-{kv.Key.EnStr}'>{kv.Key.JpStr}</h4>");
        strBldr.AppendLine("<div class='kohosya-info')><ol class='distList'>");

        foreach (var resp in kv.Value)
        {
            strBldr.AppendLine("<li><ol><li><dl>");
            strBldr.AppendLine($"<li class='kohosya-name'>{(resp.Result == "当選" ? "\ud83c\udf38 " : string.Empty)}{resp.Name}<span>（{resp.NameFurigana}）</span> ：\u3000<span>{resp.PartyName}</span></li>");
            strBldr.AppendLine($"<li class='setsumon'><b>設問（1-a）：</b><br>{resp.Q1Answer}</li>");
            strBldr.AppendLine($"<li class='setsumon'><b>設問（1-b）：</b><br>{resp.Q1Reason}</li>");
            strBldr.AppendLine($"<li class='setsumon'><b>設問（2-a）：</b><br>{resp.Q2Answer}</li>");
            strBldr.AppendLine($"<li class='setsumon'><b>設問（2-b）：</b><br>{resp.Q2Reason}</li>");
            strBldr.AppendLine($"<li class='setsumon'><b>設問（3）：</b><br>{resp.FavoriteMangaAnimeGame}</li>");
            strBldr.AppendLine("</dl></li></ol></li>");
        }

        strBldr.AppendLine("</ol></div>");
    }

    strBldr.AppendLine("</div>");
}
var 小選挙区出力 = strBldr.ToString();

File.WriteAllText(Path.Combine(directoryPath, "single-seat.html"), 小選挙区出力);

Console.WriteLine(小選挙区出力);

strBldr.Clear();

// 比例ブロックごとに候補者情報を分ける
var removeRecords = new List<Shuinsen2024Respond>();
var 比例ブロック出力内容Dic = new Dictionary<ShuinHireiBlock, List<Shuinsen2024Respond>>();
foreach (var blk in Enum.GetValues<ShuinHireiBlock>())
{
    var blockName = blk.GetDescription();

    var respondList = new List<Shuinsen2024Respond>();
    foreach (var shuinsen2024Respond in list.Where(shuinsen2024Respond => shuinsen2024Respond.Constituency == blockName))
    {
        removeRecords.Add(shuinsen2024Respond);
        respondList.Add(shuinsen2024Respond);
    }

    // 取得した分を元のリストから削除
    foreach (var shuinsen2024Respond in removeRecords)
    {
        list.Remove(shuinsen2024Respond);
    }

    比例ブロック出力内容Dic.Add(blk, respondList);
}


// 比例ブロックの出力

foreach (var kv in 比例ブロック出力内容Dic)
{
    strBldr.AppendLine($"<h3 id='hirei-{kv.Key.ToString().ToLower()}'>{kv.Key.GetDescription()}</h3>");
    strBldr.AppendLine("<div class='kohosya-info')><ol class='distList'>");

    foreach (var resp in kv.Value)
    {
        strBldr.AppendLine("<li><ol><li><dl>");
        strBldr.AppendLine($"<li class='kohosya-name'>{(resp.Result == "当選" ? "\ud83c\udf38 " : string.Empty)}{resp.Name}<span>（{resp.NameFurigana}）</span> ：\u3000<span>{resp.PartyName}</span></li>");
        strBldr.AppendLine($"<li class='setsumon'><b>設問（1-a）：</b><br>{resp.Q1Answer}</li>");
        strBldr.AppendLine($"<li class='setsumon'><b>設問（1-b）：</b><br>{resp.Q1Reason}</li>");
        strBldr.AppendLine($"<li class='setsumon'><b>設問（2-a）：</b><br>{resp.Q2Answer}</li>");
        strBldr.AppendLine($"<li class='setsumon'><b>設問（2-b）：</b><br>{resp.Q2Reason}</li>");
        strBldr.AppendLine($"<li class='setsumon'><b>設問（3）：</b><br>{resp.FavoriteMangaAnimeGame}</li>");
        strBldr.AppendLine("</dl></li></ol></li>");
    }

    strBldr.AppendLine("</div>");
}

var 比例区出力 = strBldr.ToString();

File.WriteAllText(Path.Combine(directoryPath, "proportional.html"), 比例区出力);

Console.WriteLine(比例区出力);

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
    Console.WriteLine($"好きなマンガ・アニメ・ゲーム: {resp.FavoriteMangaAnimeGame}");
    Console.WriteLine();
}

Console.WriteLine("出力が完了しました。何か入力するとプログラムが終了します。");
Console.ReadKey();