namespace ElectionPageCreateTool.Attributes
{
    public class ShugiinKuwariAttribute : Attribute
    {
        public int Kuwari { get; }

        public string Name { get; }

        public ShugiinKuwariAttribute(int kuwari, string str)
        {
            Kuwari = kuwari;
            Name = str;
        }
    }
}
