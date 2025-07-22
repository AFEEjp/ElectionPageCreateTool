namespace ElectionPageCreateTool.Attributes
{
    public class AlphabetNameAttribute(string str) : Attribute
    {
        public string Name { get; } = str;
    }
}
