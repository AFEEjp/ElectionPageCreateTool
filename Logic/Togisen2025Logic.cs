using System.Text;
using System.Text.Json;
using ElectionPageCreateTool.Extensions;
using ElectionPageCreateTool.Model.Togisen2025;

namespace ElectionPageCreateTool.Logic;

public static class Togisen2025Logic
{
    public static readonly DateTime NoticeDate = new DateTime(2025, 6, 13);

    public static void Generate(string json, string directoryPath)
    {
        // JSONをクラスのリストにパース
        var list = JsonSerializer.Deserialize<List<Togisen2025Respond>>(json);

        if (list == null)
        {
            Console.WriteLine("パース失敗？");
            return;
        }


        // カナの値を利用して並び替え
        list = list.OrderBy(r => r.Namekana).ToList();

        // 選挙区ごとに候補者情報を分ける
        var removeRecords = new List<Togisen2025Respond>();
        var 選挙区出力内容Dic = new Dictionary<Togisen2025Block, List<Togisen2025Respond>>();
        foreach (var blk in Enum.GetValues<Togisen2025Block>())
        {
            var blockName = blk.GetDescription();

            var respondList = new List<Togisen2025Respond>();
            foreach (var togisen2025Respond in list.Where(r => r.Constituency == blockName))
            {
                // スプレッドシートの値が間違っているものについて調整
                switch (togisen2025Respond.Name)
                {
                    case "原忠信":
                        togisen2025Respond.Namekana = "はらただのぶ";
                        break;
                    case "須山　たかし":
                        togisen2025Respond.PartyName = "国民民主党";
                        break;
                    case "鴇崎　直行":
                        togisen2025Respond.Name = "ときざき　直行";
                        break;
                    case "○ すどう　健太郎":
                        togisen2025Respond.Name = "すどう　健太郎";
                        break;
                    case "きとう直樹":
                        togisen2025Respond.Birthdate = "1983/10/02";
                        break;
                    case "みぞぐち晃一":
                    case "森　ひでひこ":
                    case "梅木　大輝":
                    case "太田　あつし":
                        // 不出馬なのでリストから除外
                        continue;
                }

                removeRecords.Add(togisen2025Respond);
                respondList.Add(togisen2025Respond);
            }

            // 取得した分を元のリストから削除
            foreach (var rec in removeRecords)
            {
                list.Remove(rec);
            }

            選挙区出力内容Dic.Add(blk, respondList);
        }


        var strBldr = new StringBuilder();

        // 出力

        foreach (var kv in 選挙区出力内容Dic)
        {
            strBldr.AppendLine($"<h3 id='{kv.Key.ToString()}'>{kv.Key.GetDescription()}</h3>");
            strBldr.AppendLine("<div class='kohosya-info')><ol class='distList'>");

            foreach (var resp in kv.Value)
            {
                strBldr.AppendLine("<li><ol><li><dl>");
                strBldr.AppendLine($"<li class='kohosya-name'>{(resp.Result == "当選" ? "\ud83c\udf38 " : string.Empty)}{resp.Name.Replace("　", " ")}<span>（{resp.Namekana.Replace("　", " ")}）</span> : <span>{resp.PartyName}</span></li>");

                if (Togisen2025Yakusoku.約束賛同者.Contains(resp.Namekana.Replace("　", "").Replace(" ", "")))
                {
                    strBldr.AppendLine($"<li class='yakusoku'><b>表現の自由を守るための約束 賛同者</b></li>");
                }

                // 誕生日
                if (!string.IsNullOrEmpty(resp.Birthdate) && DateTime.TryParse(resp.Birthdate, out var birthdate))
                {
                    var age = NoticeDate.Year - birthdate.Year;
                    if (birthdate > NoticeDate.AddYears(-age)) age--; // 今年の誕生日がまだ来ていない場合
                    strBldr.AppendLine($"<li class='birthdate'><b>誕生日：</b><br>{birthdate:yyyy/MM/dd} （{age}歳）</li>");
                }

                var isNoResponse = resp.AdditionalInformation is "無回答" or "回答なし";

                strBldr.AppendLine($"<li class='setsumon'><b>設問(1)：</b><br>{(isNoResponse ? "無回答" : resp.Q1Answer.ModifyQ1Answer())}</li>");
                strBldr.AppendLine($"<li class='setsumon'><b>設問(1)の理由：</b><br>{(isNoResponse ? "無回答" : resp.Q1Reason)}</li>");
                strBldr.AppendLine($"<li class='setsumon'><b>設問(2)：</b><br>{(isNoResponse ? "無回答" : resp.Q2Answer.ModifyQ2Answer())}</li>");
                strBldr.AppendLine($"<li class='setsumon'><b>設問(3)：</b><br>{resp.FavoriteMangaAnimeGame}</li>");

                if (!string.IsNullOrEmpty(resp.AdditionalInformation) && resp.AdditionalInformation != "無回答")
                    strBldr.AppendLine($"<li class='setsumon'><b>補足：</b><br>{resp.AdditionalInformation.Replace("  ", "<br>")}</li>");

                strBldr.AppendLine("</dl></li></ol></li>");


                Console.WriteLine($"名前: {resp.Name}");
                Console.WriteLine($"カナ: {resp.Namekana}");
                Console.WriteLine($"誕生日: {resp.Birthdate}");
                Console.WriteLine($"選挙区: {resp.Constituency}");
                Console.WriteLine($"所属政党: {resp.PartyName}");
                Console.WriteLine($"Q1: {resp.Q1Answer}");
                Console.WriteLine($"Q1の理由: {resp.Q1Reason}");
                Console.WriteLine($"Q2: {resp.Q2Answer}");
                Console.WriteLine($"好きなマンガ・アニメ・ゲーム: {resp.FavoriteMangaAnimeGame}");
                Console.WriteLine($"補足: {resp.AdditionalInformation}");
                Console.WriteLine($"--------------------------------------------------------");
                Console.WriteLine();
            }

            strBldr.AppendLine("</div>");
        }

        var 出力 = strBldr.ToString();

        File.WriteAllText(Path.Combine(directoryPath, "senkyoku.html"), 出力);
    }



    public static string ModifyQ1Answer(this string q1Answer)
    {
        // 特定の回答を修正
        return q1Answer switch
        {
            "安易な図書指定がなされないように条例と運用を見直すべきだ" => "A. 安易な図書指定がなされないように条例と運用を見直すべきだ",
            "現状のままでよい・わからない" => "B. 現状のままでよい・わからない",
            "さらに図書指定による規制を強化するべきだ" => "C. さらに図書指定による規制を強化するべきだ",
            _ => q1Answer
        };
    }


    public static string ModifyQ2Answer(this string q2Answer)
    {
        // 特定の回答を修正
        return q2Answer switch
        {
            "民間の自主規制が十分機能している" => "A. 民間の自主規制が十分機能している",
            "民間の自主規制がある程度機能している" => "B. 民間の自主規制がある程度機能している",
            "どちらともいえない" => "C. どちらともいえない",
            "法令による規制がある程度あった方がよい" => "D. 法令による規制がある程度あった方がよい",
            "法令による厳格な規制が必要である" => "E. 法令による厳格な規制が必要である",
            _ => q2Answer
        };
    }
}

