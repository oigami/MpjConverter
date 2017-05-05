using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MpjConverter
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {

                Console.WriteLine("ファイルパス(.xml,.mpj)を入力してください");
                string fname = Console.ReadLine();
                using (FileStream fs = new FileStream(fname, FileMode.Open))
                {

                    byte[] bytes = new byte[32];
                    fs.Read(bytes, 0, bytes.Length);
                    Encoding encoding = Encoding.GetEncoding("UTF-16");
                    string header = encoding.GetString(bytes, 0, bytes.Length).TrimEnd('\0');

                    // unknown data
                    var x = new byte[4];
                    fs.Read(x, 0, x.Length);
                    const string MMMHeader = "MMM SaveFile";
                    if (header != MMMHeader)
                    {
                        fs.Seek(0, SeekOrigin.Begin);
                        Compress(fname, fs);
                    }
                    else
                    {
                        fs.Seek(38, SeekOrigin.Begin);
                        Decompress(fname, fs);
                    }
                }
                Console.WriteLine("正常に終了しました。");
                Console.ReadLine();
            }
            catch (Exception ex)
            {
                Console.WriteLine("エラーが発生しました。");
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.StackTrace);
                Console.ReadLine();
            }
        }

        private static void Compress(string fname, FileStream fs)
        {
            string mpjPath = fname + ".mpj";
            Console.WriteLine("Output :" + mpjPath);
            if (CheckOutputFile(mpjPath) == false) return;
            BinaryWriter fso = new BinaryWriter(new FileStream(mpjPath, FileMode.Create));
            var bytes = Encoding.GetEncoding("UTF-16").GetBytes("MMM SaveFile");

            fso.Write(bytes, 0, bytes.Length);
            fso.Seek(32, SeekOrigin.Begin);
            fso.Flush();
            var mem = new MemoryStream();
            using (DeflateStream ds = new DeflateStream(mem, CompressionMode.Compress))
            {
                byte[] data = new byte[1024];

                while (true)
                {
                    int rs = fs.Read(data, 0, data.Length);
                    ds.Write(data, 0, rs);
                    if (rs == 0) break;
                }
            }
            var arr = mem.ToArray();
            fso.Write(arr.Length + 2 + 0x10);
            fso.Write(new byte[] { 0x78, 0x9c }, 0, 2);
            fso.Write(arr);
        }

        private static void Decompress(string fname, FileStream fs)
        {
            DeflateStream ds = new DeflateStream(fs, CompressionMode.Decompress);

            string xmlPath = fname + ".xml";
            Console.WriteLine("Output :" + xmlPath);
            if (CheckOutputFile(xmlPath) == false) return;
            using (FileStream fso = new FileStream(xmlPath, FileMode.Create))
            {
                byte[] data = new byte[512];
                while (true)
                {
                    int rs = ds.Read(data, 0, data.Length);
                    fso.Write(data, 0, rs);
                    if (rs == 0) break;
                }
            }
        }


        private static bool CheckOutputFile(string headerWritePath)
        {
            if (File.Exists(headerWritePath))
            {
                while (true)
                {
                    Console.WriteLine("すでに出力ファイルが存在しています。上書きしますか？[y/n]");
                    var yn = Console.ReadLine();
                    if (yn == "n") return false;
                    if (yn == "y") return true;
                }
            }
            return true;
        }
    }
}
