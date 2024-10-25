namespace ElectionPageCreateTool.Model
{
    public class Shuinsen2024PrefEntry
    {
        public string 小選挙区名 { get; set; } = string.Empty;

        public List<Shuinsen2024Respond> RespondList { get; set; } = new List<Shuinsen2024Respond>();
    }
}
