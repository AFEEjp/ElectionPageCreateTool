using System.Text.Json;

namespace ElectionPageCreateTool
{
    public static class TsvFileConvert
    {
        public static (string json, string directoryPath) ConvertToJson()
        {
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
                return (string.Empty, string.Empty);
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
                var options = new JsonSerializerOptions
                {
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                    WriteIndented = true
                };
                json = JsonSerializer.Serialize(jsonList, options);
            }

            File.WriteAllText(Path.Combine(directoryPath, $"{DateTime.Now:yyyyMMddHHmmss}.json"), json);

            return (json, directoryPath);
        }
    }
}
