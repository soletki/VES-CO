using System.IO;


namespace VESCO.Timeline
{
    public class SourceFile
    {
        public string FilePath { get; set; }

        public SourceFile(string filePath)
        {
            FilePath = filePath;
        }
        public string FileName => Path.GetFileName(FilePath);
    }
}
