using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using TaMi_Automatenclient.UI.Layout;

namespace TaMi_Automatenclient
{
    public class KassenclientBaseForm : Form
    {
        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern IntPtr CreateRoundRectRgn(int nLeftRect, int nTopRect, int nRightRect, int nBottomRect, int nWidthEllipse, int nHeightEllipse);


        public static readonly Color HeaderAccent1 = Color.FromArgb(33, 150, 243),  //Color.FromArgb(33, 150, 243),
                                     HeaderAccent2 = Color.FromArgb(25, 118, 210); //Color.FromArgb(33, 203, 243);

        public static readonly Color BackcolorNeutral = Color.FromArgb(33, 150, 243);

        public static readonly Font HeaderTextFont = new Font("Segoe UI Variable", 16F, FontStyle.Bold),
                                    HeaderButtonFont = new Font("Segoe UI Symbol", 14F, FontStyle.Bold),

                                    ButtonFont = new Font("Segoe UI Variable", 12F, FontStyle.Regular);

        public static readonly Color FormBordercolor = Color.FromArgb(13, 71, 161);


        private ModernHeaderPanel _modernHeader;


        public void SetupDefaultForm(string Name, string title, Size size)
        {
            this.Name = Name;

            this.Text = title;
            this.Icon = Program.AppIcon;

            this.FormBorderStyle = FormBorderStyle.None;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.ShowInTaskbar = false;
            this.ClientSize = size;
            
            this.BackColor = Color.WhiteSmoke;

            this.AutoScroll = true;
            this.DoubleBuffered = true;
            this.KeyPreview = true;


            //try { form.Region = Region.FromHrgn(CreateRoundRectRgn(0, 0, form.Width, form.Height + 12, 12, 12)); } catch { }

            // Zeichne einen Rahmen um die gesamte Form
            this.Paint += (s, e) =>
            {
                using (var pen = new Pen(FormBordercolor))
                {
                    e.Graphics.DrawRectangle(pen, 0, 0, this.ClientSize.Width - 1, this.ClientSize.Height - 1);
                }
            };

            // Bei Größe ändern neu zeichnen
            this.Resize += (s, e) => this.Invalidate();

            //form.KeyDown += PersonalForm_KeyDown;
            //form.KeyPress += PersonalForm_KeyPress;

        }

        public void AddHeaderPanel(string title)
        {
            AddHeaderPanel(title, false, false, false);
        }

        public void AddHeaderPanel(string title, bool closeButton, bool minimizeButton, bool canMoveForm)
        {
            if (_modernHeader != null)
            {
                try { Controls.Remove(_modernHeader); } catch { }
                try { _modernHeader.Dispose(); } catch { }
                _modernHeader = null;
            }

            if (!closeButton && !minimizeButton && !canMoveForm)
                return;

            _modernHeader = new ModernHeaderPanel
            {
                Title = title,
                ShowMinimize = minimizeButton
            };

            if (!canMoveForm)
            {
                _modernHeader.MouseDown -= null;
                _modernHeader.MouseMove -= null;
            }

            _modernHeader.CloseClicked += () => { if (closeButton) { try { Close(); } catch { } } };
            _modernHeader.MinimizeClicked += () => { if (minimizeButton) { try { WindowState = FormWindowState.Minimized; } catch { } } };

            Controls.Add(_modernHeader);
            _modernHeader.BringToFront();
        }

    }
}
