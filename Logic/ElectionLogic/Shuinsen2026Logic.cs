using System.Text;
using ElectionPageCreateTool.Extensions;
using ElectionPageCreateTool.Model;
using ElectionPageCreateTool.Model.Shuinsen2026;
using Microsoft.Extensions.Configuration;

namespace ElectionPageCreateTool.Logic.ElectionLogic;

public static class Shuinsen2026Logic
{
    public static readonly DateTime NoticeDate = new DateTime(2026, 1, 20);

    public static void MainLogic()
    {
        while (true)
        {
            Console.Clear();

            Console.WriteLine("処理を選択してください:");
            Console.WriteLine("1. ExcelファイルをSQLiteに保存");
            Console.WriteLine("2. サイト生成");
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

            var dbLogic = new DatabaseOperationLogic();
            var tableName = nameof(Shuinsen2026Respond);
            Console.WriteLine($"データベースファイル: {dbLogic.GetDbFilePath()}");
            Console.WriteLine($"テーブル名: {tableName}");
            dbLogic.ShowUpsertKeyColumns<Shuinsen2026Respond>();

            dbLogic.UpsertDataToSQLite<Shuinsen2026Respond>(tableName, dic);
        }

        Console.ReadKey();
    }

    private static void Generate()
    {
        // 環境変数から出力先ディレクトリを取得
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        var directoryPath = configuration["OutputSettings:DirectoryPath"];

        // 環境変数に設定がない場合は入力を求める
        if (string.IsNullOrEmpty(directoryPath))
        {
            Console.WriteLine("出力先ディレクトリを入力してください:");
            directoryPath = Console.ReadLine();

            if (string.IsNullOrEmpty(directoryPath))
            {
                Console.WriteLine("出力先ディレクトリが指定されていません。");
                return;
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
            strBldr.AppendLine($"<h3 id='pref-{vt.都道府県名.ToString().ToLower()}'>{vt.都道府県名.GetDescription()}</h3>");
            strBldr.AppendLine("<div>");

            foreach (var kv in vt.候補者情報リスト)
            {
                strBldr.AppendLine($"<h4 id='kuwari-{kv.Key.EnStr}'>{kv.Key.JpStr}</h4>");
                strBldr.AppendLine("<div class='kohosya-info')><ol class='distList'>");

                foreach (var resp in kv.Value)
                {
                    strBldr.AppendCandidateInfoHtml(resp);
                }

                strBldr.AppendLine("</ol></div>");
            }

            strBldr.AppendLine("</div>");
        }
        var 小選挙区出力 = strBldr.ToString();

        File.WriteAllText(Path.Combine(directoryPath, "single-seat.html"), 小選挙区出力);

        Console.WriteLine("小選挙区HTMLを出力しました。");

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
            strBldr.AppendLine($"<h3 id='hirei-{kv.Key.ToString().ToLower()}'>{kv.Key.GetDescription()}</h3>");
            strBldr.AppendLine("<div class='kohosya-info')><ol class='distList'>");

            foreach (var resp in kv.Value)
            {
                strBldr.AppendCandidateInfoHtml(resp);
            }

            strBldr.AppendLine("</div>");
        }

        var 比例区出力 = strBldr.ToString();

        File.WriteAllText(Path.Combine(directoryPath, "proportional.html"), 比例区出力);

        Console.WriteLine("比例区HTMLを出力しました。");

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
        strBuilder.AppendLine("<li><ol><li><dl>");

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
        strBuilder.AppendLine($"<li class='setsumon'><b>設問（2-a）：</b>{FormatQ2AnswerAsList(resp.Q2Answer)}</li>");
        strBuilder.AppendLine($"<li class='setsumon'><b>設問（2-b）：</b>{resp.Q2Reason}</li>");
        strBuilder.AppendLine($"<li class='setsumon'><b>設問（3）：</b>{resp.FavoriteMangaAnimeGame}</li>");
        strBuilder.AppendLine("</dl></li></ol></li>");

        return strBuilder;
    }

    /// <summary>
    /// 設問（2-a）の回答をアルファベット付きリストにフォーマット
    /// </summary>
    private static string FormatQ2AnswerAsList(string q2Answer)
    {
        if (string.IsNullOrWhiteSpace(q2Answer))
        {
            return string.Empty;
        }

        // 選択肢とアルファベットの対応（アンケートフォームの順番）
        var choiceOrder = new Dictionary<string, string>
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
            .OrderBy(a => choiceOrder.TryGetValue(a, out var order) ? order : "Z")
            .ToList();

        // HTMLリストを生成
        var listItems = sortedAnswers
            .Select(a => choiceOrder.TryGetValue(a, out var prefix) ? $"<li>{prefix}. {a}</li>" : $"<li>{a}</li>");

        return $"<ul class='q2-list'>{string.Join("", listItems)}</ul>";
    }
}