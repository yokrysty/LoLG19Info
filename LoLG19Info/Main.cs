using System;
using System.Windows.Forms;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Diagnostics;
using System.Reflection;


namespace LoLG19Info
{
    public partial class Main : Form
    {
        private const string TRAY_TEXT = "G19 - League of Legends";

        private const int LCD_SCREEN_LOADING = 0;
        private const int LCD_SCRERN_INFO_TEAM1 = 1;
        private const int LCD_SCREEN_INFO_TEAM2 = 2;

        //The fastest you should send updates to the LCD is around 30fps or 34ms.
        //100ms is probably a good typical update speed.
        private int connection = DMcLgLCD.LGLCD_INVALID_CONNECTION;
        private int device = DMcLgLCD.LGLCD_INVALID_DEVICE;
        private int deviceType = DMcLgLCD.LGLCD_INVALID_DEVICE;
        private Bitmap LCD;
        private uint button;

        private Bitmap[] Screens = new Bitmap[3];
        private int currentScreen = LCD_SCREEN_LOADING;
        CancellationTokenSource tokenSource1 = new CancellationTokenSource();
        private bool parsed = false;
        private string LCDScreenshotFolder;

        private string summoner = "YourSummonerName";
        private string region = "EUNE";

        public Main()
        {
            InitializeComponent();
            this.notifyIcon1.Text = TRAY_TEXT;
            this.WindowState = FormWindowState.Minimized;
            this.Visible = false;
            this.ShowInTaskbar = false;
            this.Hide();
        }

        private void Main_Load(object sender, EventArgs e)
        {
            this.LoadConfig();

           if (DMcLgLCD.LcdInit() == DMcLgLCD.ERROR_SUCCESS)
            {
                this.connection = DMcLgLCD.LcdConnectEx("League Of Legends Info", 0, 0);

                if (this.connection != DMcLgLCD.LGLCD_INVALID_CONNECTION)
                {
                    this.device = DMcLgLCD.LcdOpenByType(this.connection, DMcLgLCD.LGLCD_DEVICE_QVGA);

                    if (this.device == DMcLgLCD.LGLCD_INVALID_DEVICE)
                    {
                        this.device = DMcLgLCD.LcdOpenByType(this.connection, DMcLgLCD.LGLCD_DEVICE_BW);
                        if (this.device != DMcLgLCD.LGLCD_INVALID_DEVICE)
                        {
                            this.deviceType = DMcLgLCD.LGLCD_DEVICE_BW;
                        }
                    }
                    else
                    {
                        this.deviceType = DMcLgLCD.LGLCD_DEVICE_QVGA;
                    }

                   if (this.deviceType == DMcLgLCD.LGLCD_DEVICE_QVGA)
                    {
                        this.Screens[LCD_SCREEN_LOADING] = getImage(@"images\splash.jpg");
                        Graphics g = Graphics.FromImage(this.Screens[LCD_SCREEN_LOADING]);
                        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                        g.DrawString("Awaiting game launch...", new Font("Arial", 12, FontStyle.Regular, GraphicsUnit.Pixel), Brushes.White, 92, 220);
                        g.Dispose();

                        this.Render();
                    }

                    if (this.deviceType > 0)
                    {
                        this.MainLoopTask(this.tokenSource1.Token);
                    }
                }
            }
        }

        private void LoadConfig()
        {
            string config = "config.txt";
            if (!File.Exists(config))
            {
                MessageBox.Show("Can't find " + config + ". Application will close.", "Configuration file missing", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                Application.Exit();
            }
            else
            {
                using (StreamReader reader = new StreamReader(config))
                {
                    for (int i = 1; i <= 2; i++)
                    {
                        string str = reader.ReadLine();
                        if (str != null)
                        {
                            if (i == 1)
                            {
                                this.summoner = str;
                            }
                            else if (i == 2)
                            {
                                this.region = str;
                            }
                        }
                    }
                }

                if (this.summoner.Equals("YourSummonerName"))
                {
                    MessageBox.Show("Before using this application you have to write your own summoner name in the config.txt file. Application will close.", "Summoner name", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    Application.Exit();
                }
                this.LCDScreenshotFolder = string.Format(@"{0}\screenshots\", Application.StartupPath);
                if (!Directory.Exists(this.LCDScreenshotFolder))
                {
                    Directory.CreateDirectory(this.LCDScreenshotFolder);
                }
            }
        }

        private static bool isLeagueOpen()
        {
            foreach (Process process in Process.GetProcesses())
            {
                if (process.ProcessName.Contains("League of Legends"))
                {
                    return true;
                }
            }
            return false;
        }

        private void DisconnectLCD()
        {
            if (this.LCD != null)
            {
                this.LCD.Dispose();
                DMcLgLCD.LcdClose(this.device);
                DMcLgLCD.LcdDisconnect(this.connection);
                DMcLgLCD.LcdDeInit();
            }
        }

        private void MainLoopTask(CancellationToken token)
        {
            Task.Factory.StartNew(() =>
            {
                try
                {
                    token.ThrowIfCancellationRequested();
                    while (true)
                    {
                        token.ThrowIfCancellationRequested();
                        
                        if (isLeagueOpen() && this.connection != DMcLgLCD.LGLCD_INVALID_CONNECTION)
                        {
                            this.LCDButtonAction();

                            if (!this.parsed)
                            {
                                this.Render();
                                Thread.Sleep(1500);
                                this.getLoLNexusInfo();
                                this.parsed = true;
                            }
                        }
                        else
                        {
                            this.parsed = false;
                            this.currentScreen = LCD_SCREEN_LOADING;
                            Thread.Sleep(300);
                            if (this.timer1.Enabled)
                            {
                                this.Invoke((MethodInvoker)delegate { this.timer1.Enabled = false; });
                            }
                        }
                    }
                }
                catch(Exception)
                {
                }
            }, token);
        }

        private void getLoLNexusInfo()
        {
            LoLNexusParser parser = new LoLNexusParser();
            parser.Parse(this.summoner, this.region);

            if (parser.Success)
            {
                Array data = parser.getData();

                int i = LCD_SCRERN_INFO_TEAM1;
                foreach (LoLNexusInfo[] summoners in data)
                {
                    if (summoners.Length > 0)
                    {
                        this.Screens[i] = getTeamInfoImage(summoners);
                    }
                    i++;
                }
                this.currentScreen = LCD_SCRERN_INFO_TEAM1;
                if (!this.timer1.Enabled)
                {
                    this.Invoke((MethodInvoker)delegate { this.timer1.Enabled = true; });
                }
            }
        }

        private void Render(int LCDType = DMcLgLCD.LGLCD_DEVICE_QVGA)
        {
            if (this.Screens[this.currentScreen] == null)
            {
                return;
            }

            this.LCD = this.Screens[this.currentScreen];
            if (this.currentScreen > LCD_SCREEN_LOADING)
            {
                addHintText(ref this.LCD);
            }
            DMcLgLCD.LcdUpdateBitmap(this.device, this.LCD.GetHbitmap(), LCDType);
            DMcLgLCD.LcdSetAsLCDForegroundApp(this.device, DMcLgLCD.LGLCD_FORE_YES);
        }

        private void LCDButtonAction()
        {
            if (this.currentScreen == LCD_SCREEN_LOADING)
            {
                return;
            }

            uint LCDbtn = DMcLgLCD.LcdReadSoftButtons(this.device);
            if (this.button != LCDbtn)
            {
                this.button = LCDbtn;
                if (this.button == DMcLgLCD.LGLCD_BUTTON_OK)
                {
                    if ((this.currentScreen != this.Screens.Length - 1) && (this.currentScreen >= LCD_SCRERN_INFO_TEAM1))
                    {
                        this.currentScreen++;
                    }
                    else
                    {
                        this.currentScreen--;
                    }
                }
            }
        }

        private static Bitmap getImage(string path, bool notFoundMark = false, int width = 320, int height = 240)
        {
            if (File.Exists(path))
            {
                Image img = Image.FromFile(path);
                if (img.Width == width && img.Height == height)
                {
                    return (Bitmap)img;
                }
                else
                {
                    return new Bitmap(img, new Size(width, height));
                }
            }

            Bitmap bmp = new Bitmap(width, height, PixelFormat.Format24bppRgb);
            Graphics g = Graphics.FromImage(bmp);
            g.Clear(Color.Black);
            if (notFoundMark)
            {
                Pen pen = new Pen(Brushes.White);
                g.DrawLine(pen, new Point(0, 0), new Point(width, height));
                g.DrawLine(pen, new Point(width, 0), new Point(0, height));
                pen.Dispose();
            }
            g.Dispose();

            return bmp;
        }

        private static Bitmap getTeamInfoImage(LoLNexusInfo[] summoners)
        {
            int championImageWidth = 40;
            int championImageHeight = 40;

            int spellImageWidth = 17;
            int spellImageHeight = 17;

            int marginTop = 15;
            int marginLeftRight = 5;

            int distanceBetweenRows = 5;
            int distance1 = 4;
            int distance2 = 8;
            int column2Offset = 25;

            Bitmap bmp = getImage(@"images\background.jpg");

            using (Graphics g = Graphics.FromImage(bmp))
            {
                Font font = new Font("Arial", 10, FontStyle.Regular, GraphicsUnit.Pixel);
                StringFormat strFormat = new StringFormat();
                strFormat.Alignment = StringAlignment.Far;

                int rowOffsetY = marginTop;
                for (int i = 0; i <= summoners.Length - 1; i++)
                {
                    string championName = summoners[i].Champion.ToLower().Replace("'", "").Replace(" ", String.Empty).Replace(".", String.Empty);

                    Bitmap championImage = getImage(@"images\champions\" + championName + ".jpg", true, championImageWidth, championImageHeight);
                    Bitmap spell1Image = getImage(@"images\spells\" + summoners[i].Spells[0].ToLower() + ".jpg", true, spellImageWidth, spellImageHeight);
                    Bitmap spell2Image = getImage(@"images\spells\" + summoners[i].Spells[1].ToLower() + ".jpg", true, spellImageWidth, spellImageHeight);

                    int spellImageX = marginLeftRight + championImage.Width + distance1;
                    int spellImage2Y = rowOffsetY + spell1Image.Height + (championImage.Height - 2 * spell1Image.Height);

                    g.DrawImage(championImage, marginLeftRight, rowOffsetY, championImage.Width, championImage.Height);
                    g.DrawImage(spell1Image, spellImageX, rowOffsetY, spell1Image.Width, spell1Image.Height);
                    g.DrawImage(spell2Image, spellImageX, spellImage2Y, spell2Image.Width, spell2Image.Height);

                    int bottomLineXStart = marginLeftRight + championImage.Width + distance1 + spell1Image.Width + distance2;
                    int bottomLineLength = bmp.Width - bottomLineXStart - marginLeftRight;
                    int bottomLineXEnd = bottomLineXStart + bottomLineLength;
                    int bottomLineY = rowOffsetY + championImage.Height;

                    if (i != summoners.Length - 1)
                    {
                        g.DrawLine(new Pen(Color.White), new Point(bottomLineXStart, bottomLineY), new Point(bottomLineXEnd, bottomLineY));
                    }

                    int textColumn2X = bottomLineXStart + bottomLineLength / 3 + column2Offset;

                    g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                    g.DrawString(summoners[i].Summoner, font, (summoners[i].me) ? Brushes.Red : Brushes.White, bottomLineXStart, rowOffsetY);
                    g.DrawString(summoners[i].Champion, font, Brushes.White, bottomLineXStart, spellImage2Y);
                    g.DrawString(summoners[i].Level, font, Brushes.White, textColumn2X, rowOffsetY);
                    g.DrawString(summoners[i].Division, font, Brushes.White, textColumn2X, spellImage2Y);
                    g.DrawString(summoners[i].Wins + " Wins", font, Brushes.White,
                        new Rectangle(bottomLineXStart, spellImage2Y, bottomLineLength, font.Height), strFormat);

                    rowOffsetY = rowOffsetY + championImage.Height + distanceBetweenRows;
                }
                strFormat.Dispose();
                font.Dispose();
            }
            return bmp;
        }

        private static void addHintText(ref Bitmap bmp)
        {
            using (Graphics g = Graphics.FromImage(bmp))
            {
                StringFormat strFormat = new StringFormat();
                strFormat.Alignment = StringAlignment.Far;
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                g.DrawString("OK - switch screen", new Font("Arial", 9, FontStyle.Regular, GraphicsUnit.Pixel),
                    Brushes.White, new Rectangle(0, 2, bmp.Width - 2, bmp.Height), strFormat);
                strFormat.Dispose();
            }
        }

        private void refreshInfoToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!isLeagueOpen())
            {
                MessageBox.Show("Game is not running.", "Game is not running", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                this.getLoLNexusInfo();
            }
        }

        private void lCDScreenshotToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (this.LCD != null)
            {
                string filename = String.Format("{0}LCD-Screenshot-{1:dd-MM-yyyy_HH-mm-ss}.png", this.LCDScreenshotFolder, DateTime.Now);
                this.LCD.Save(filename, ImageFormat.Png);
                MessageBox.Show("Screenshot saved to " + filename, "LCD screenshot saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show("Can't get image from LCD!", "No image on LCD", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }
        }

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AssemblyCopyrightAttribute copyright = Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyCopyrightAttribute), false)[0] as AssemblyCopyrightAttribute;
            string info = Application.ProductName + "\n" + "version " + Application.ProductVersion + "\n" + copyright.Copyright;
            MessageBox.Show(info);
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void Main_FormClosing(object sender, FormClosingEventArgs e)
        {
            this.DisconnectLCD();
            this.tokenSource1.Cancel();
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            this.Render();
        }
    }
}