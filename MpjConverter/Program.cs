using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using zlib;

namespace MpjConverter
{
    internal static class Program
    {
        static void Main(string[] args)
        {
            try
            {
                if (args.Length >= 1)
                {
                    foreach (var s in args)
                    {
                        Run(s);
                    }

                    return;
                }

                Console.WriteLine("ファイルパス(.xml,.mpj)を入力してください");
                string fname = Console.ReadLine();
                Run(fname);
                Console.WriteLine("正常に終了しました。");
            }
            catch (Exception ex)
            {
                Console.WriteLine("エラーが発生しました。");
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.StackTrace);
            }

            Console.WriteLine("");
            Console.Read();
        }

        private static void Run(string fname)
        {
            using (var fs = new FileStream(fname, FileMode.Open))
            {
                var bytes = new byte[32];
                fs.Read(bytes, 0, bytes.Length);
                var encoding = Encoding.GetEncoding("UTF-16");
                string header = encoding.GetString(bytes, 0, bytes.Length).TrimEnd('\0');

                // zliデータの圧縮時の合計サイズ
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
                    fs.Seek(36, SeekOrigin.Begin);
                    Decompress(fname, fs, BitConverter.ToInt32(x, 0) - bytes.Length - 4);
                }
            }
        }
        #region Compress

        private static byte[] CompressZ(byte[] inbuf, int level = 6) //CompLevel= 0-9
        {
            var z = new ZStream();
            var ms = new MemoryStream();
            var outbuf = new byte[1024];

            if (z.deflateInit(level) != zlibConst.Z_OK)
            {
                throw new InvalidOperationException("zlib.deflateInit");
            }

            z.next_in = inbuf;
            z.avail_in = inbuf.Length;
            z.next_in_index = 0;

            z.next_out = outbuf;
            z.avail_out = outbuf.Length;
            z.next_out_index = 0;

            while (true)
            {
                int status = z.deflate(zlibConst.Z_FINISH); /* 圧縮する */
                if (status == zlibConst.Z_STREAM_END)
                {
                    break;
                }

                if (status != zlibConst.Z_OK)
                {
                    throw new InvalidOperationException("zlib.deflate");
                }

                if (z.avail_out == 0)
                {
                    ms.Write(outbuf, 0, outbuf.Length);
                    z.next_out = outbuf;
                    z.avail_out = outbuf.Length;
                    z.next_out_index = 0;
                }
            }

            if ((outbuf.Length - z.avail_out) != 0)
            {
                ms.Write(outbuf, 0, outbuf.Length - z.avail_out);
            }

            if (z.deflateEnd() != zlibConst.Z_OK)
            {
                throw new InvalidOperationException("zlib.deflateEnd");
            }

            z.free();

            return ms.ToArray();
        }

        private static byte[] ReadByteAll(FileStream fs)
        {
            byte[] outBuf;
            using (var tmp = new MemoryStream())
            {
                fs.CopyTo(tmp);
                tmp.Close();
                outBuf = tmp.ToArray();
            }
            return outBuf;
        }

        private static void Compress(string fname, FileStream fs)
        {
            string mpjPath = fname + ".mpj";
            Console.WriteLine("Output :" + mpjPath);
            if (!CheckOutputFile(mpjPath))
            {
                return;
            }

            using (var fso = new BinaryWriter(new FileStream(mpjPath, FileMode.Create)))
            {
                var bytes = Encoding.GetEncoding("UTF-16").GetBytes("MMM SaveFile");

                fso.Write(bytes);
                fso.Seek(32, SeekOrigin.Begin);
                fso.Flush();

                byte[] deflate = CompressZ(ReadByteAll(fs));
                var memoryStream = new MemoryStream();
                memoryStream.Write(deflate, 0, deflate.Length);
                string workDir = Path.GetDirectoryName(fname);

                // TODO: 最大で幾つになるか不明
                for (int i = 1; ; i++)
                {
                    string dat = Path.Combine(workDir, $"data{i}.dat");
                    if (!File.Exists(dat))
                    {
                        break;
                    }

                    var data = File.ReadAllBytes(dat);

                    deflate = CompressZ(data);
                    Console.WriteLine($"data{i}.dat size: {data.Length}\t compress size: {deflate.Length}");
                    memoryStream.Write(deflate, 0, deflate.Length);
                }

                fso.Write((int) memoryStream.Length + 36);
                Console.WriteLine($"all deflate size: {memoryStream.Length}");
                fso.Write(memoryStream.ToArray());

                string footer = Path.Combine(workDir, "footer.dat");
                fso.Write(File.ReadAllBytes(footer));
            }
        }

        #endregion

        #region Decompress

        private static (byte[] bytes, int res) DecompressZ(byte[] inbuf, int index = 0)
        {
            var z = new ZStream();
            var ms = new MemoryStream();
            var outbuf = new byte[1024];

            if (z.inflateInit() != zlibConst.Z_OK)
            {
                throw new InvalidOperationException("zlib.inflateInit Error");
            }

            z.next_in = inbuf;
            z.avail_in = inbuf.Length;
            z.next_in_index = index;

            z.next_out = outbuf;
            z.avail_out = outbuf.Length;
            z.next_out_index = 0;

            while (true)
            {
                int status = z.inflate(zlibConst.Z_NO_FLUSH); /* 展開 */
                if (status == zlibConst.Z_STREAM_END)
                {
                    break;
                }

                if (status != zlibConst.Z_OK)
                {
                    throw new InvalidOperationException("zlib.inflate Error");
                }

                if (z.avail_out == 0)
                {
                    ms.Write(outbuf, 0, outbuf.Length);
                    z.next_out = outbuf;
                    z.avail_out = outbuf.Length;
                    z.next_out_index = 0;
                }
            }

            if ((outbuf.Length - z.avail_out) != 0)
            {
                ms.Write(outbuf, 0, outbuf.Length - z.avail_out);
            }

            if (z.inflateEnd() != zlibConst.Z_OK)
            {
                throw new InvalidOperationException("zlib.inflateEnd Error");
            }

            int count = z.next_in_index - index;
            z.free();

            return (ms.ToArray(), count);
        }

        private static void Decompress(string fname, FileStream fs, int size)
        {
            var outBuf = ReadByteAll(fs);
            var filename = Path.GetFileNameWithoutExtension(fname);
            string workDir;
            {
                var dir = Path.GetDirectoryName(fname);
                workDir = Path.Combine(dir, filename);
            }
            Directory.CreateDirectory(workDir);
            string xmlPath = Path.Combine(workDir, "data0.xml");
            Console.WriteLine("Output :" + xmlPath);
            if (!CheckOutputFile(xmlPath))
            {
                return;
            }
            // TODO: 最大で幾つになるか不明
            for (int i = 0; ; i++)
            {
                string path = Path.Combine(workDir, $"data{i}.dat");
                if (!File.Exists(path))
                {
                    break;
                }

                File.Delete(path);
            }

            for (int index = 0, i = 0; index < size; ++i)
            {
                var (buf, p) = DecompressZ(outBuf, index);
                index += p;
                File.WriteAllBytes(Path.Combine(workDir, $"data{i}.") + (i == 0 ? "xml" : "dat"), buf);
                Console.WriteLine($"{filename}/data{i}.{(i == 0 ? "xml" : "dat")} decompress size: {buf.Length}");
            }

            if (size < outBuf.Length)
            {
                using (var fso = File.OpenWrite(Path.Combine(workDir, $"footer.dat")))
                {
                    fso.Write(outBuf, size, outBuf.Length - size);
                }
            }
        }

        #endregion

        private static bool CheckOutputFile(string headerWritePath)
        {
            if (File.Exists(headerWritePath))
            {
                while (true)
                {
                    Console.WriteLine("すでに出力ファイルが存在しています。上書きしますか？[y/n]");
                    switch (Console.ReadLine())
                    {
                        case "n":
                            return false;
                        case "y":
                            return true;
                    }
                }
            }

            return true;
        }
    }
}
