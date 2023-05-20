namespace JPEGCompressor
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Compress (1) or decompress (2)?");
            string input = Console.ReadLine();
            Console.WriteLine("Enter file path:");
            string path = Console.ReadLine();
            string ext = Path.GetExtension(path);
            if(input == "1")
            {
                Console.WriteLine("Enter compress quality (1-10):");
                int quality = int.Parse(Console.ReadLine());
                MyJPEGConverter.ConvertToMyJPEG(path, path.Replace(ext, ".myjpeg"), quality);
            } else if (input == "2")
            {
                MyJPEGConverter.ConvertToBMP(path, path.Replace(ext, ".bmp"));
            }
            Console.WriteLine("Done!");
        }
    }
}