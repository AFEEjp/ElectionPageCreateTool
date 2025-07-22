using System.Text;
using System.Text.Json;
using ElectionPageCreateTool.Extensions;
using ElectionPageCreateTool.Model.Saninsen2025;

namespace ElectionPageCreateTool.Logic;

public static class Saninsen2025Logic
{
    public static readonly DateTime NoticeDate = new DateTime(2025, 7, 3);

    public static void GenerateSaninsen2025(string json, string directoryPath)
    {
        // JSONをクラスのリストにパース
        var list = JsonSerializer.Deserialize<List<Saninsen2025Respond>>(json);

        if (list == null)
        {
            Console.WriteLine("パース失敗？");
            return;
        }


        // 同じ候補者からの回答がある場合の調整
        list = list
             .GroupBy(x => new { x.Name, x.Constituency, x.PartyName })
             .Select(g =>
             {
                 // 全てのアンケート回答が同じかどうか判定
                 var first = g.First();
                 var allSame = g.All(x =>
                     x.Q1Answer == first.Q1Answer &&
                     x.Q1Reason == first.Q1Reason &&
                     x.Q2Answer == first.Q2Answer &&
                     x.Q2Reason == first.Q2Reason &&
                     x.FavoriteMangaAnimeGame == first.FavoriteMangaAnimeGame
                 );

                 // 全て同じなら1件だけ、異なるなら最後のものを選択
                 return allSame ? first : g.Last();
             }).ToList();


        // ふりがなの値を利用して並び替え
        list = list.OrderBy(r => r.NameFurigana).ToList();

        // 選挙区ごとに候補者情報を分ける
        var 選挙区出力内容Dic = new Dictionary<Senkyoku2025Enum, List<Saninsen2025Respond>>();
        foreach (var senkyoku in Enum.GetValues<Senkyoku2025Enum>())
        {
            var respList = new List<Saninsen2025Respond>();
            foreach (var resp in list.Where(r => r.Constituency == senkyoku.GetAbbreviation()))
            {
                // スプレッドシートの値が間違っているものについて調整
                switch (resp.Name)
                {
                    default:
                        break;
                }

                respList.Add(resp);
            }

            // 取得した分を元のリストから削除
            list.RemoveAll(item => respList.Contains(item));

            選挙区出力内容Dic.Add(senkyoku, respList);
        }


        var strBldr = new StringBuilder();

        foreach (var (選挙区, 選挙区リスト) in 選挙区出力内容Dic)
        {
            strBldr.AppendLine($"<h3 id='pref-{選挙区.ToString().ToLower()}'>{選挙区.GetDescription()}</h3>");
            strBldr.AppendLine("<div>");

            foreach (var resp in 選挙区リスト)
            {
                strBldr.AppendCandidateInfoHtml(resp);
            }

            strBldr.AppendLine("</div>");
        }
        var 選挙区出力 = strBldr.ToString();

        File.WriteAllText(Path.Combine(directoryPath, "single-seat.html"), 選挙区出力);

        Console.WriteLine(選挙区出力);

        strBldr.Clear();



        // 比例ブロックごとに候補者情報を分ける
        var removeRecords = new List<Saninsen2025Respond>();

        var 比例出力内容Dic = new Dictionary<HireiParty2025, List<Saninsen2025Respond>>();
        foreach (var party in Enum.GetValues<HireiParty2025>())
        {
            var respList = new List<Saninsen2025Respond>();
            foreach (var saninsen2025Respond in list.Where(r => r.PartyName == party.GetDescription()))
            {
                // スプレッドシートの値が間違っているものについて調整
                switch (saninsen2025Respond.Name)
                {
                    default:
                        break;
                }

                removeRecords.Add(saninsen2025Respond);
                respList.Add(saninsen2025Respond);
            }

            // 取得した分を元のリストから削除
            foreach (var rec in removeRecords)
            {
                list.Remove(rec);
            }

            比例出力内容Dic.Add(party, respList);
        }

        foreach (var (政党名, 全国比例リスト) in 比例出力内容Dic)
        {
            strBldr.AppendLine($"<h3 id='hirei-{政党名.GetAlphabetName()}'>{政党名.GetDescription()}</h3>");
            strBldr.AppendLine("<div>");

            foreach (var resp in 全国比例リスト)
            {
                strBldr.AppendCandidateInfoHtml(resp);
            }

            strBldr.AppendLine("</div>");
        }

        var 比例区出力 = strBldr.ToString();

        File.WriteAllText(Path.Combine(directoryPath, "proportional.html"), 比例区出力);

        Console.WriteLine(比例区出力);

        foreach (var resp in list)
        {
            Console.WriteLine($"名前: {resp.Name.Trim().Replace("　", " ")}");
            Console.WriteLine($"ふりがな: {resp.NameFurigana.Trim().Replace("　", " ")}");
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

        Console.WriteLine($"小選挙区、比例区どちらにも該当しないデータ:{list.Count}件");

    }


    public static StringBuilder AppendCandidateInfoHtml(this StringBuilder strBuilder, Saninsen2025Respond resp)
    {
        strBuilder.AppendLine("<div class='kohosya-info')><ol class='distList'>");

        strBuilder.AppendLine("<li><ol><li><dl>");

        var name = $"{resp.Name.Trim().Replace("　", " ")}<span>（{resp.NameFurigana.Trim().Replace("　", " ")}）</span>";
        var hrefAnchor = $"{resp.PartyName}_{resp.Name.Trim().Replace("　", "").Replace(" ", "").Replace("\u3000", "")}";

        strBuilder.AppendLine($"<li class='kohosya-name' id='{hrefAnchor}'>{(resp.Result == "当選" ? "\ud83c\udf38 " : string.Empty)} <a class='name' href='#{hrefAnchor}'>{name}</a> ：\u3000<span>{resp.PartyName}</span></li>");

        // 誕生日
        if (!string.IsNullOrEmpty(resp.Birthdate) && DateTime.TryParse(resp.Birthdate, out var birthdate))
        {
            var age = NoticeDate.Year - birthdate.Year;
            if (birthdate > NoticeDate.AddYears(-age)) age--;
            strBuilder.AppendLine($"<li class='birthdate'><b>誕生日：</b><br><div class='answer'>{birthdate:yyyy/MM/dd} （{age}歳）</div></li>");
        }

        strBuilder.AppendLine($"<li class='setsumon'><b>設問（1-a）：</b><br><div class='answer'>{resp.Q1Answer.ModifyQ1Answer()}</div></li>");
        strBuilder.AppendLine($"<li class='setsumon'><b>設問（1-b）：</b><br><div class='answer'>{resp.Q1Reason}</div></li>");

        // Q2Answerをパースしてリスト化
        var q2aAnsList = resp.Q2Answer.ParseQ2Answer();

        var q2AnswerOutput = string.Join("<br>", q2aAnsList);



        strBuilder.AppendLine($"<li class='setsumon'><b>設問（2-a）：</b><br><div class='answer'>{q2AnswerOutput}</div></li>");
        strBuilder.AppendLine($"<li class='setsumon'><b>設問（2-b）：</b><br><div class='answer'>{resp.Q2Reason}</div></li>");
        strBuilder.AppendLine($"<li class='setsumon'><b>設問（3）：</b><br><div class='answer'>{resp.FavoriteMangaAnimeGame}</div></li>");
        strBuilder.AppendLine("</dl></li></ol></li>");

        strBuilder.AppendLine("</ol></div>");
        return strBuilder;
    }


    private static string ModifyQ1Answer(this string q1Answer)
    {
        // 特定の回答を修正
        return q1Answer switch
        {
            "法令で規制すべき" => "A. 法令で規制すべき",
            "法令で規制するべきではない" => "B. 法令で規制するべきではない",
            "どちらともいえない、答えない" => "C. どちらともいえない、答えない",
            _ => q1Answer
        };
    }

    private static List<string> ParseQ2Answer(this string q2Answer)
    {
        var resultList = new List<string>();

        // 選択肢の配列
        string[] choices = new string[]
        {
            "刑法のわいせつ物頒布規制",
            "AV新法による規制",
            "クレジットカード決済の制約",
            "過度なジェンダー平等論や多様性への配慮に基づく表現規制",
            "新サイバー犯罪条約による創作物への規制",
            "いわゆる「エロ広告」等、不適切とされる広告への法的規制",
            "国連女子差別撤廃委員会の勧告による表現規制",
            "表現の自由を不当に制限していると思うものは特にない"
        };

        // 残りのテキスト（処理途中で更新されます）
        string remainingText = q2Answer;

        // 各選択肢について確認
        foreach (var choice in choices)
        {
            if (remainingText.Contains(choice))
            {
                // 選択肢が見つかったらリストに追加
                resultList.Add(choice.ModifyQ2Answer());

                // 既に処理した選択肢を残りのテキストから削除
                // まず選択肢そのものを削除
                remainingText = remainingText.Replace(choice, "");

                // 選択肢の後の ", " または "," を削除（最初の1つのみ）
                remainingText = remainingText.TrimStart();
                if (remainingText.StartsWith(", "))
                    remainingText = remainingText.Substring(2);
                else if (remainingText.StartsWith(","))
                    remainingText = remainingText.Substring(1);
            }
        }

        // 処理後に残ったテキストがあれば（自由記述部分）、それも追加
        remainingText = remainingText.Trim();
        if (!string.IsNullOrEmpty(remainingText))
        {
            if (remainingText == "無回答")
            {
                resultList.Add("無回答");
            }
            else
            {
                resultList.Add($"I:自由記述<br>「{remainingText}」");
            }
        }

        return resultList;
    }

    private static string ModifyQ2Answer(this string q2Answer)
    {
        // 特定の回答を修正
        return q2Answer switch
        {
            "刑法のわいせつ物頒布規制" => "A. 刑法のわいせつ物頒布規制",
            "AV新法による規制" => "B. AV新法による規制",
            "クレジットカード決済の制約" => "C. クレジットカード決済の制約",
            "過度なジェンダー平等論や多様性への配慮に基づく表現規制" => "D. 過度なジェンダー平等論や多様性への配慮に基づく表現規制",
            "新サイバー犯罪条約による創作物への規制" => "E. 新サイバー犯罪条約による創作物への規制",
            "いわゆる「エロ広告」等、不適切とされる広告への法的規制" => "F. いわゆる「エロ広告」等、不適切とされる広告への法的規制",
            "国連女子差別撤廃委員会の勧告による表現規制" => "G. 国連女子差別撤廃委員会の勧告による表現規制",
            "表現の自由を不当に制限していると思うものは特にない" => "H. 表現の自由を不当に制限していると思うものは特にない",
            _ => q2Answer
        };
    }
}