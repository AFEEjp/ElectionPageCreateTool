namespace ElectionPageCreateTool.Attributes
{
    public class AbbreviationAttribute(string str) : Attribute
    {
        public string Name { get; } = str;
    }
}
