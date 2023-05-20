using System.Drawing;

namespace JPEGCompressor
{
    public static class MyJPEGConverter
    {
        public static int blocks_encoded = 0;

        public static byte[,] Y_t;
        public static byte[,] Cb_t;
        public static byte[,] Cr_t;


        public static void ConvertToMyJPEG(string input_file, string output_file, int a = 8)
        {
            Bitmap image = new Bitmap(input_file);
            byte[,] Y, Cb, Cr;
            RGBtoYCbCr(image, out Y, out Cb, out Cr);
            Cb = Subsampling(Cb);
            Cr = Subsampling(Cr);

            Y_t = new byte[image.Width, image.Height];
            Cb_t = new byte[image.Width / 2, image.Height / 2];
            Cr_t = new byte[image.Width / 2, image.Height / 2];
            Array.Copy(Y, Y_t, Y.Length);
            Array.Copy(Cb, Cb_t, Cb.Length);
            Array.Copy(Cr, Cr_t, Cr.Length);

            byte[][,] colors = new byte[3][,];
            colors[0] = Y;
            colors[1] = Cb;
            colors[2] = Cr;
            using(BinaryWriter output = new BinaryWriter(new FileStream(output_file, FileMode.Create)))
            {
                //encode quality
                output.Write(Convert.ToByte(a));
                //encode image size
                output.Write(Convert.ToInt16(Y.GetLength(0)));
                output.Write(Convert.ToInt16(Y.GetLength(1)));
                //process every array
                for (int i = 0; i < 3; i++)
                {
                    //process block by block
                    for (int x = 0; x < colors[i].GetLength(0) - 7; x += 8)
                    {
                        for (int y = 0; y < colors[i].GetLength(1) - 7; y += 8)
                        {
                            int[,] block = GetBlock(colors[i], x, y, 8);
                            block = DCT(block);
                            block = Quantization(block, a);
                            byte[] result = RLE(block);
                            output.Write(result);
                            blocks_encoded++;
                        }
                    }
                }
            }
        }

        public static void ConvertToBMP(string input_file, string output_file)
        {
            byte[][,] colors = new byte[3][,];
            int img_height;
            int img_width;
            using (BinaryReader input = new BinaryReader(new FileStream(input_file, FileMode.Open)))
            {
                int a = Convert.ToInt32(input.ReadByte());
                img_width = Convert.ToInt32(input.ReadInt16());
                img_height = Convert.ToInt32(input.ReadInt16());
                colors[0] = new byte[img_width, img_height]; //Y
                colors[1] = new byte[img_width / 2, img_height / 2];//Cb
                colors[2] = new byte[img_width / 2, img_height / 2];//Cr
                int[] n_blocks = new int[3];
                n_blocks[0] = img_width * img_height / 64;
                n_blocks[1] = n_blocks[0] / 4;
                n_blocks[2] = n_blocks[0] / 4;
                //reconstructing YCbCr
                for(int i = 0; i < 3; i++)
                {
                    int x = 0;
                    int y = 0;
                    //block by block
                    for (int j = 0; j < n_blocks[i]; j++)
                    {
                        List<short> block_str = new List<short>();
                        //decoding RLE
                        while (block_str.Count < 64)
                        {
                            short curr = input.ReadInt16();
                            if (curr == short.MaxValue)
                            {
                                byte counter = input.ReadByte();
                                short c = input.ReadInt16();
                                while (counter > 0)
                                {
                                    block_str.Add(c);
                                    counter--;
                                }
                            }
                            else
                            {
                                block_str.Add(curr);
                            }
                        }
                        int[,] block = ZigZagFill(block_str);
                        //Reverse quantization
                        block = ReverseQuantization(block, a);
                        //Reverse DCT
                        block = ReverseDCT(block);
                        //Reconstructing whole matrix
                        SetBlock(block, colors[i], x, y, 8);
                        y += 8;
                        if(y >= colors[i].GetLength(1))
                        {
                            y = 0;
                            x += 8;
                        }
                        if(x >= colors[i].GetLength(0))
                        {
                            x = 0;
                        }
                    }
                }
            }
            Bitmap image = new Bitmap(img_width, img_height);
            for(int x = 0; x < img_width; x++)
            {
                for(int y = 0; y < img_height; y++)
                {
                    double Y = Convert.ToDouble(colors[0][x, y]);
                    double Cb = Convert.ToDouble(colors[1][x / 2, y / 2]);
                    double Cr = Convert.ToDouble(colors[2][x / 2, y / 2]);
                    int R = Math.Max(0, Math.Min(255, Convert.ToInt32(Y * 1.0 + (Cb - 128) * 0.0 + (Cr - 128) * 1.402)));
                    int G = Math.Max(0, Math.Min(255, Convert.ToInt32(Y * 1.0 - (Cb - 128) * 0.34414 - (Cr - 128) * 0.71414)));
                    int B = Math.Max(0, Math.Min(255, Convert.ToInt32(Y * 1.0 + (Cb - 128) * 1.772 + (Cr - 128) * 0.0)));
                    Color color = Color.FromArgb(R, G, B);
                    image.SetPixel(x, y, color);
                }
            }
            image.Save(output_file);
        }

        public static int[,] GetBlock(byte[,] array, int x, int y, int d)
        {
            int[,] result = new int[d, d];
            for(int i = 0; i < d; i++)
            {
                for (int j = 0; j < d; j++)
                {
                    result[i, j] = array[x + i, y + j] - 128;
                }
            }
            return result;
        }

        public static void SetBlock(int[,] block, byte[,] destination, int x, int y, int d)
        {
            for (int i = 0; i < d; i++)
            {
                for (int j = 0; j < d; j++)
                {
                    destination[x + i, y + j] = Convert.ToByte(Math.Max(0, Math.Min(block[i, j] + 128, 255)));
                }
            }
        }

        public static void RGBtoYCbCr(Bitmap image, out byte[,] Y, out byte[,] Cb, out byte[,] Cr)
        {
            Y = new byte[image.Width, image.Height];
            Cb = new byte[image.Width, image.Height];
            Cr = new byte[image.Width, image.Height];
            for(int x = 0; x < image.Width; x++)
            {
                for(int y = 0; y < image.Height; y++)
                {
                    Color pixel = image.GetPixel(x, y);
                    double r = Convert.ToDouble(pixel.R);
                    double g = Convert.ToDouble(pixel.G);
                    double b = Convert.ToDouble(pixel.B);
                    Y[x, y] = Convert.ToByte(0.299 * r + 0.587 * g + 0.114 * b);
                    Cb[x, y] = Convert.ToByte(-0.1687 * r - 0.3313* g + 0.5 * b + 128.0);
                    Cr[x, y] = Convert.ToByte(0.5 * r - 0.4187 * g - 0.0813 * b + 128.0);
                }
            }
        }

        public static byte[,] Subsampling(byte[,] array)
        {
            byte[,] result = new byte[array.GetLength(0) / 2, array.GetLength(1) / 2];
            for(int x = 0; x < result.GetLength(0); x++)
            {
                for (int y = 0; y < result.GetLength(1); y++)
                {
                    result[x, y] = Convert.ToByte((
                        array[x * 2, y * 2] + 
                        array[x * 2 + 1, y * 2] + 
                        array[x * 2 , y * 2 + 1] +
                        array[x * 2 + 1, y * 2 + 1]) / 4);
                }
            }
            return result;
        }

        public static double C(int i, int u)
        {
            double result = (u == 0 ? 1.0 / Math.Sqrt(2) : 1.0) * Math.Cos(((2 * i + 1) * u * Math.PI) / 16.0);
            return result;
        }

        public static int[,] DCT(int[,] block)
        {
            int[,] result = new int[8, 8];

            for(int u = 0; u < 8; u++)
            {
                for(int v = 0; v < 8; v++)
                {
                    double s = 0.0;
                    for(int x = 0; x < 8; x++)
                    {
                        for(int y = 0; y < 8; y++)
                        {
                            s += block[x, y] * C(x, u) * C(y, v);
                        }
                    }
                    s *= 0.25;
                    result[u, v] = Convert.ToInt32(s);
                }
            }
            return result;
        }

        public static int[,] ReverseDCT(int[,] block)
        {
            int[,] result = new int[8, 8];

            for (int x = 0; x < 8; x++)
            {
                for (int y = 0; y < 8; y++)
                {
                    double s = 0.0;
                    for (int u = 0; u < 8; u++)
                    {
                        for (int v = 0; v < 8; v++)
                        {
                            s += block[u, v] * C(x, u) * C(y, v);
                        }
                    }
                    s *= 0.25;
                    result[x, y] = Convert.ToInt32(s);
                }
            }
            return result;
        }

        public static int[,] Quantization(int[,] block, int quality)
        {
            int[,] table = QuantizationTables.GetQuantizationTable(quality);
            int[,] result = new int[8, 8];
            for (int x = 0; x < 8; x++)
            {
                for(int y = 0; y < 8; y++)
                {
                    result[x, y] = block[x, y] / table[x, y];
                }
            }
            return result;
        }

        public static byte[] RLE(int[,] block)
        {
            int[] block_str = ZigZagScan(block);
            List<byte> result = new List<byte>();

            short separator = short.MaxValue;
            short last = Convert.ToInt16(block_str[0]);
            byte counter = 1;
            for(int i = 1; i < 64; i++)
            {
                short curr = Convert.ToInt16(block_str[i]);
                if (curr != last)
                {
                    if (counter > 2)
                    {
                        result.AddRange(BitConverter.GetBytes(separator));
                        result.Add(counter);
                        result.AddRange(BitConverter.GetBytes(last));
                    }
                    else
                    {
                        while (counter > 0)
                        {
                            result.AddRange(BitConverter.GetBytes(last));
                            counter--;
                        }
                    }
                    last = curr;
                    counter = 1;
                }
                else
                {
                    counter++;
                }
            }
            if (counter > 2)
            {
                result.AddRange(BitConverter.GetBytes(separator));
                result.Add(counter);
                result.AddRange(BitConverter.GetBytes(last));
            }
            else
            {
                while (counter > 0)
                {
                    result.AddRange(BitConverter.GetBytes(last));
                    counter--;
                }
            }

            return result.ToArray();
        }

        public static int[] ZigZagScan(int[,] block)
        {
            int[] result = new int[64];
            int index = 0;
            int i = 0;
            int j = 0;
            bool direction = false; // false = down-right, true = up-left
            while (i != 8 && j != 8)
            {
                result[index] = block[i, j];
                index++;
                if(direction)
                {
                    //up-left
                    if (j == 7)
                    {
                        i++;
                        direction = !direction;
                    }
                    else if (i == 0)
                    {
                        j++;
                        direction = !direction;
                    }
                    else
                    {
                        i--;
                        j++;
                    }
                } else
                {
                    //down-right
                    if (i == 7)
                    {
                        j++;
                        direction = !direction;
                    }
                    else if (j == 0)
                    {
                        i++;
                        direction = !direction;
                    }
                    else
                    {
                        i++;
                        j--;
                    }
                }
            }
            return result;
        }

        public static int[,] ZigZagFill(List<short> block_str)
        {
            int[,] result = new int[8, 8];
            int index = 0;
            int i = 0;
            int j = 0;
            bool direction = false; // false = down-right, true = up-left
            while (i != 8 && j != 8)
            {
                result[i, j] = block_str[index];
                index++;
                if (direction)
                {
                    //up-left
                    if (j == 7)
                    {
                        i++;
                        direction = !direction;
                    }
                    else if (i == 0)
                    {
                        j++;
                        direction = !direction;
                    }
                    else
                    {
                        i--;
                        j++;
                    }
                }
                else
                {
                    //down-right
                    if (i == 7)
                    {
                        j++;
                        direction = !direction;
                    }
                    else if (j == 0)
                    {
                        i++;
                        direction = !direction;
                    }
                    else
                    {
                        i++;
                        j--;
                    }
                }
            }

            return result;
        }

        public static int[,] ReverseQuantization(int[,] block, int quality)
        {
            int[,] table = QuantizationTables.GetQuantizationTable(quality);
            int[,] result = new int[8, 8];
            for (int x = 0; x < 8; x++)
            {
                for (int y = 0; y < 8; y++)
                {
                    result[x, y] = block[x, y] * table[x, y];
                }
            }
            return result;
        }
    }
}