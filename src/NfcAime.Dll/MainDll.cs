using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;


namespace NfcAime.Dll {
    public class MainDll {

        public static AimeReader reader;
        static byte[] idm = null;
        static string accessCode = null;
        static AimeReader.CardKind cardKind = AimeReader.CardKind.Null;
        [DllImport("kernel32.dll")]
        private static extern void AllocConsole();
        //返回API版本
        [DllExport("aime_io_get_api_version", CallingConvention = CallingConvention.StdCall)]
        public static ushort GetApiVersion() => 0x0100;

        [DllExport("aime_io_init", CallingConvention = CallingConvention.StdCall)]
        public static int Init()
        {
            AllocConsole();

            // 读取如 "1.0.0-a1b2c3d-master" 这种详细版本信息
            var versionString = Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion;

            Console.WriteLine($"PN532 Aime Reader - Version {versionString}");
            Console.WriteLine("Make With Love By ZCROM - FROM MikuNet");
            Console.WriteLine("---------------------------------------------");
            Console.WriteLine($"{Config.ReaderCOM}   Baud:{Config.ReaderBaud}   Mode:{(Config.IDmMode == 1 ? "IDmMode" : "AccessCodeMode")}");
            reader = new AimeReader(port: Config.ReaderCOM, baud: Config.ReaderBaud);
            return 0;
        }

        //卡轮询
        [DllExport("aime_io_nfc_poll", CallingConvention = CallingConvention.StdCall)]
        public static int NfcPoll(byte unitNo)
        {
            Console.WriteLine(">> Polling...");
            (cardKind, idm, accessCode) = reader.ReadCard();
            return 0;
        }

        //获取Aime AccessCode
        [DllExport("aime_io_nfc_get_aime_id", CallingConvention = CallingConvention.StdCall)]
        public static int GetAimeId(byte unitNo, IntPtr luid, nint luidSize)
        {

            if (unitNo != 0)
            {
                return 1;
            }
            if (accessCode == null)
            {
                return 1;
            }

            if (Config.IDmMode == 1 && cardKind == AimeReader.CardKind.Felica)
            {
                return 1;
            }
            if (cardKind == AimeReader.CardKind.Null)
            {
                return 1;
            }

            //将卡号复制到缓存区以传递给游戏
            Marshal.Copy(AccessCodeFormatter.ToAccessCodeBytes(accessCode), 0, luid, (int)luidSize);
            Console.WriteLine("# " + cardKind + " !!");
            Console.WriteLine("<< AccessCode");
            return 0;
        }

        //获取FeliCa ID
        [DllExport("aime_io_nfc_get_felica_id", CallingConvention = CallingConvention.StdCall)]
        public static unsafe int GetFelicaId(byte unitNo, ulong* iDM)
        {
            if (idm == null)
            {
                return 1;
            }

            if (cardKind == AimeReader.CardKind.Felica && Config.IDmMode == 1) //防止传入M1卡
            {
                ulong idmValue = 0;
                for (var i = 0; i < 8; i++)
                {
                    idmValue = (idmValue << 8) | idm[i];
                }

                *iDM = idmValue;
                Console.WriteLine("<< IDm");
                return 0;
            }
            return 1;
        }

        //设置LED颜色
        [DllExport("aime_io_led_set_color", CallingConvention = CallingConvention.StdCall)]
        public static void SetLedColour(byte unitNo, byte r, byte g, byte b)
        {
        }
    }
}