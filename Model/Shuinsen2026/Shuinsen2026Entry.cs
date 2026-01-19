namespace ElectionPageCreateTool.Model.Shuinsen2026;

public class Saninsen2026Entry
{
    public string 選挙区名 { get; set; } = string.Empty;

    public List<Shuinsen2026Respond> RespondList { get; set; } = new List<Shuinsen2026Respond>();
}