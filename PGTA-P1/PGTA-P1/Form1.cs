using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace PGTA_P1
{
    public partial class Form1 : Form
    {
        List<DataBlock> DataBlockList;
        List<DataTable> DataTable1000;
        int numDTable = 0;

        string CatView = "All";
        string SourView = "All";
        string IdView = "All";

        //Moure ventana
        public const int WM_NCLBUTTONDOWN = 0xA1;
        public const int HT_CAPTION = 0x2;
        [DllImportAttribute("user32.dll")]
        public static extern int SendMessage(IntPtr hWnd,int Msg, int wParam, int lParam);
        [DllImportAttribute("user32.dll")]
        public static extern bool ReleaseCapture();

        //Constructor
        public Form1()
        {
            InitializeComponent();

            this.openFileDialog = new System.Windows.Forms.OpenFileDialog();

            PGB1.Minimum = 1;
        }

        private DataView FiltrarCatSour()
        {
            DataTable Inicial = DataTable1000[numDTable];
            DataRow[] F = new DataRow[999];
            DataTable Filtrada = new DataTable();
            Filtrada.Columns.Add("Category");
            Filtrada.Columns.Add("Source");
            Filtrada.Columns.Add("Target ID/Address/T.Number");
            Filtrada.Columns.Add("Vehicle Fleet");
            Filtrada.Columns.Add("DataBlock Id");

            if ((CatView == "All") && (SourView == "All"))
                Filtrada = Inicial;
            else if (CatView == "All") //Radar no es all
            {
                F = Inicial.Select("Source = '" + SourView + "'");
                int i = 0;
                while (i < F.Count())
                {
                    Filtrada.ImportRow(F[i]);
                    i++;
                }
            }
            else if (SourView == "All") //Cat no es all
            {
                F = Inicial.Select("Category = '" + CatView + "'");
                int i = 0;
                while (i < F.Count())
                {
                    Filtrada.ImportRow(F[i]);
                    i++;
                }
            }
            else //Cap es all
            {
                F = Inicial.Select("Category = '" + CatView + "' AND Source = '" + SourView + "'");
                int i = 0;
                while (i < F.Count())
                {
                    Filtrada.ImportRow(F[i]);
                    i++;
                }
            }
            DataView ret = Filtrada.DefaultView;

            previousBTT.Visible = true;
            nextBTN.Visible = true;
            Max.Text = Convert.ToString(this.DataTable1000.Count());

            return ret;
        }

        private DataView FiltrarID()
        {
            DataTable Final = new DataTable();
            Final.Columns.Add("Category");
            Final.Columns.Add("Source");
            Final.Columns.Add("Target ID/Address/T.Number");
            Final.Columns.Add("Vehicle Fleet");
            Final.Columns.Add("DataBlock Id");
            numDTable = 0;
            while(numDTable<DataTable1000.Count())
            {
                DataTable Input = FiltrarCatSour().ToTable();
                DataRow[] F = new DataRow[999];
                F = Input.Select("[Target ID/Address/T.Number] LIKE '" + IdView + "%'");
                int j = 0;
                while (j < F.Count())
                {
                    Final.ImportRow(F[j]);
                    j++;
                }
                numDTable++;
            }
            numDTable = 0;
            
            DataView ret = Final.DefaultView;
            ret.Sort = "[Target ID/Address/T.Number]";

            Max.Text = "1";
            previousBTT.Visible = false;
            nextBTN.Visible = false;

            return ret;
        }

        //Actualització de DGV DataBlocks
        private void DataBlocksDGV_Act()
        {
            DataInf.Text = "Loading...";
            DataInf.ForeColor = Color.DarkGray;
            DataInf.Refresh();
            DataView Filtrada = FiltrarCatSour();
            if (IdView != "All")
                Filtrada = FiltrarID();

            this.Cursor = Cursors.WaitCursor;   
            DataBlocksAll.DataSource = Filtrada.ToTable();
            DataBlocksAll.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
            this.Cursor = Cursors.Default;
            DataInf.Text = "Data loaded";
            DataInf.ForeColor = Color.Green;
            DataInf.Refresh();
        }

        //Actualització de DGV DataBlockView
        private void DataBlockViwerDGV_Act(DataBlock Element)
        {
            DataBlocViwer.Columns.Clear();
            DataBlocViwer.Rows.Clear();
            DataBlocViwer.ColumnCount = 3;
            DataBlocViwer.Columns[0].Name = "Item name";
            DataBlocViwer.Columns[1].Name = "Message (DeCod)";
            DataBlocViwer.Columns[2].Name = "Units";
            

            //Obrim els datafields
            int i = 0;
            while (i < Element.DataFields.Count())
            {
                DataField Visio = Element.DataFields[i];
                if (Visio.DeCode.Count != 0)
                {
                    DataBlocViwer.Rows.Add(Visio.LinVectNom());
                    int h = 1;
                    while (h < Visio.DeCode.Count)
                    {
                        DataBlocViwer.Rows.Add(Visio.LinVect(h));
                        h++;
                    }
                }
                i++;
            }

            DataBlocViwer.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
        }

        //BTN sortida
        private void Exit_Click(object sender, EventArgs e)
        {
            this.Close();
        }
        private void pictureBox1_MouseHover(object sender, EventArgs e)
        {
            pictureBox1.Image = Image.FromFile("S3(hover).png");
        }
        private void pictureBox1_MouseLeave(object sender, EventArgs e)
        {
            pictureBox1.Image = Image.FromFile("S3.png");
        }

        //BTN Load
        private void LoadBTN_MouseHover(object sender, EventArgs e)
        {
            LoadBTN.BackColor = Color.FromArgb(0, 66, 108);
        }
        private void LoadBTN_MouseLeave(object sender, EventArgs e)
        {
            LoadBTN.BackColor = Color.FromArgb(209, 222, 230);
        }
        private void LoadBTN_Click(object sender, EventArgs e)
        {
            DataInf.Text = "Loading...";
            DataInf.ForeColor = Color.DarkGray;
            pictureBox5.BringToFront();
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                InitialDirectory = @"D:\",
                Title = "Browse Files",

                CheckFileExists = true,
                CheckPathExists = true,
                Filter = "ast files (*.ast)|*.ast",
                DefaultExt = ".ast",
                FilterIndex = 2,
                RestoreDirectory = true,

                ReadOnlyChecked = true,
                ShowReadOnly = true
            };


            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                TextVisorBTN.BorderStyle = BorderStyle.None;
                PanelControlSuperior.Visible = false;
                TextVisorPanel.Visible = false;
                this.DataBlockList = new List<DataBlock>();//lista con paquetes separados
                this.DataTable1000 = new List<DataTable>();
                this.Cursor = Cursors.WaitCursor;
                PGB1.Visible = true;
                byte[] Bytes = File.ReadAllBytes(openFileDialog.FileName); //vector bytes todos juntos, sin separar ni nada
                CatLib[] Cat = Hertz_Hülsmeyer.CarregarCategories();
                PGB1.Maximum = Bytes.Count();
                int H = 0; //Contador delements no inscrits a CAT10 i CAT21
                PGB1.Value = 1;

                bool Cat10 = false; bool Cat21 = false; bool Psr = false; bool Multi = false;

                DataTable DT = new DataTable();
                DT.Columns.Add("Category");
                DT.Columns.Add("Source");
                DT.Columns.Add("Target ID/Address/T.Number");
                DT.Columns.Add("Vehicle Fleet");
                DT.Columns.Add("DataBlock Id");

                int i = 0; int numDT = 0;
                while (i < Bytes.Count())
                {
                    //Obtenirm dades inicials del block
                    string CAT = Bytes[i].ToString();
                    if (CAT == "10")
                        Cat10 = true;
                    else
                        Cat21 = true;
                    int Long = Convert.ToInt32(Bytes[i + 2].ToString());
                    Queue<byte> BytesSave = new Queue<byte>();


                    //Introduim tots els bytes dins d'una queue per crear el DataBlock
                    int j = 0;
                    while (j < Long)
                    {
                        BytesSave.Enqueue(Bytes[j + i]); //Afegim a la llista local
                        j++;
                    }

                    //Si es de la categoria desitjada l'enllistem a la llista general
                    if ((CAT == "10") || (CAT == "21"))
                    {
                        DataBlockList.Add(new DataBlock(BytesSave, Cat, DataBlockList.Count())); //Afegim a la llista general
                        
                        if (DataBlockList.Last().From == "Multi.")
                            Multi = true;
                        else if (DataBlockList.Last().From == "SMR")
                            Psr = true;
                            numDT++;
                        if (numDT == 999)
                        {
                            this.DataTable1000.Add(DT);
                            DT = new DataTable();
                            DT.Columns.Add("Category");
                            DT.Columns.Add("Source");
                            DT.Columns.Add("Target ID/Address/T.Number");
                            DT.Columns.Add("Vehicle Fleet");
                            DT.Columns.Add("DataBlock Id");
                            numDT = 0;
                        }

                        DT.Rows.Add(DataBlockList.Last().StringLin());
                    }
                    else
                    {
                        H++;
                    }

                    i = i + j;
                    PGB1.Step = j;
                    PGB1.PerformStep();
                }
                this.DataTable1000.Add(DT);
                this.Cursor = Cursors.Default;
                PGB1.Visible = false;
                PanelControlSuperior.Visible = true;
                DataInf.Text = "Data loaded";
                DataInf.ForeColor = Color.Green;
                FileName.Text = "(File: " + openFileDialog.FileName + ")";
                FileName.Visible = true;
                Current.Text = "1";
                Max.Text = Convert.ToString(this.DataTable1000.Count());
                TextVisorPanel.BringToFront();

                if ((Cat10 == true) && (Cat21 == true))
                {
                    Cat010BTN.Visible = true;
                    AllCatBTN.Visible = true;
                    Cat021BTN.Visible = true;
                    AdsBTN.Visible = true;
                    SourView = "All";
                    CatView = "All";
                    if ((Multi == true) && (Psr == true))
                    {
                        MultiBTN.Visible = true;
                        AllSBTN.Visible = true;
                        PSRBTN.Visible = true;
                    }
                    else if (Multi == true)
                    {
                        MultiBTN.Visible = true;
                        AllSBTN.Visible = true;
                        PSRBTN.Visible = false;
                    }
                    else
                    {
                        MultiBTN.Visible = false;
                        AllSBTN.Visible = true;
                        PSRBTN.Visible = true;
                    }
                }
                else if (Cat10 == true)
                {
                    Cat010BTN.Visible = true;
                    AllCatBTN.Visible = false;
                    Cat021BTN.Visible = false;
                    AdsBTN.Visible = false;
                    Cat010BTN.BorderStyle = BorderStyle.FixedSingle;
                    if ((Multi == true) && (Psr == true))
                    {
                        MultiBTN.Visible = true;
                        PSRBTN.Visible = true;
                        AllSBTN.Visible = true;
                        SourView = "All";
                        CatView = "10";
                    }
                    else if (Multi == true)
                    {
                        MultiBTN.Visible = true;
                        PSRBTN.Visible = false;
                        AllSBTN.Visible = false;
                        MultiBTN.BorderStyle = BorderStyle.FixedSingle;
                        SourView = "Multi.";
                        CatView = "10";
                    }
                    else
                    {
                        MultiBTN.Visible = false;
                        PSRBTN.Visible = true;
                        AllSBTN.Visible = false;
                        SourView = "SMR";
                        CatView = "10";
                        PSRBTN.BorderStyle = BorderStyle.FixedSingle;
                    }
                }
                else
                {
                    Cat010BTN.Visible = false;
                    MultiBTN.Visible = false;
                    PSRBTN.Visible = false;
                    AllCatBTN.Visible = false;
                    Cat021BTN.Visible = true;
                    AdsBTN.Visible = true;
                    AllSBTN.Visible = false;
                    SourView = "ADS-B";
                    CatView = "21";
                    Cat021BTN.BorderStyle = BorderStyle.FixedSingle;
                    AdsBTN.BorderStyle = BorderStyle.FixedSingle;
                }
            }
            else
            {
                DataInf.Text = "No data loaded";
                DataInf.ForeColor = Color.Red;
                TextVisorPanel.BringToFront();
                if (DataBlockList.Count != 0)
                {
                    DataInf.Text = "Data loaded";
                    DataInf.ForeColor = Color.Green;
                }
            }
        }

        //Moure finestra
        private void BarraSuperior_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
            }
        }

        //BTN TextVisor
        private void TextVisorBTN_Click(object sender, EventArgs e)
        {
            DataBlocksDGV_Act();
            TextVisorPanel.Visible = true;
            TextVisorBTN.BorderStyle = BorderStyle.FixedSingle;
            MapVisor.BorderStyle = BorderStyle.None;
        }
        private void TextVisorBTN_MouseHover(object sender, EventArgs e)
        {
            TextVisorBTN.BackColor = Color.FromArgb(0, 66, 108);
        }
        private void TextVisorBTN_MouseLeave(object sender, EventArgs e)
        {
            TextVisorBTN.BackColor = Color.FromArgb(209, 222, 230);
        }

        //BTN MapVisor
        private void MapVisor_Click(object sender, EventArgs e)
        {
            TextVisorBTN.BorderStyle = BorderStyle.None;
            MapVisor.BorderStyle = BorderStyle.FixedSingle;
        }
        private void MapVisor_MouseHover(object sender, EventArgs e)
        {
            MapVisor.BackColor = Color.FromArgb(0, 66, 108);
        }
        private void MapVisor_MouseLeave(object sender, EventArgs e)
        {
            MapVisor.BackColor = Color.FromArgb(209, 222, 230);
        }

        //BTN Cat010
        private void Cat010BTN_Click(object sender, EventArgs e)
        {
            CatView = "10";
            Cat010BTN.BorderStyle = BorderStyle.FixedSingle;
            Cat021BTN.BorderStyle = BorderStyle.None;
            AllCatBTN.BorderStyle = BorderStyle.None;

            DataBlocksDGV_Act();
        }
        private void Cat010BTN_MouseHover(object sender, EventArgs e)
        {
            Cat010BTN.BackColor = Color.FromArgb(0,66, 108);

        }
        private void Cat010BTN_MouseLeave(object sender, EventArgs e)
        {
            Cat010BTN.BackColor = Color.FromArgb(209, 222, 230);
        }

        //BTN Cat021
        private void Cat021BTN_Click(object sender, EventArgs e)
        {
            CatView = "21";
            Cat010BTN.BorderStyle = BorderStyle.None;
            Cat021BTN.BorderStyle = BorderStyle.FixedSingle;
            AllCatBTN.BorderStyle = BorderStyle.None;

            DataBlocksDGV_Act();
        }
        private void Cat021BTN_MouseHover(object sender, EventArgs e)
        {
            Cat021BTN.BackColor = Color.FromArgb(0, 66, 108);
        }
        private void Cat021BTN_MouseLeave(object sender, EventArgs e)
        {
            Cat021BTN.BackColor = Color.FromArgb(209, 222, 230);
        }

        //BTN AllCat
        private void AllCatBTN_Click(object sender, EventArgs e)
        {
            CatView = "All";
            Cat010BTN.BorderStyle = BorderStyle.None;
            Cat021BTN.BorderStyle = BorderStyle.None;
            AllCatBTN.BorderStyle = BorderStyle.FixedSingle;

            DataBlocksDGV_Act();
        }
        private void AllCatBTN_MouseHover(object sender, EventArgs e)
        {
            AllCatBTN.BackColor = Color.FromArgb(0, 66, 108);
        }
        private void AllCatBTN_MouseLeave(object sender, EventArgs e)
        {
            AllCatBTN.BackColor = Color.FromArgb(209, 222, 230);
        }

        //BTN Next
        private void nextBTN_Click(object sender, EventArgs e)
        {
            if (numDTable < DataTable1000.Count())
                numDTable++;
            DataBlocksDGV_Act();
            Current.Text = Convert.ToString(numDTable + 1);
        }
        private void nextBTN_MouseHover(object sender, EventArgs e)
        {
            nextBTN.BackColor = Color.FromArgb(0, 66, 108);
        }
        private void nextBTN_MouseLeave(object sender, EventArgs e)
        {
            nextBTN.BackColor = Color.FromArgb(209, 222, 230);
        }

        //BTN previous
        private void previousBTT_Click(object sender, EventArgs e)
        {
            if (numDTable != 0)
                numDTable--;
            DataBlocksDGV_Act();
            Current.Text = Convert.ToString(numDTable + 1);
        }
        private void previousBTT_MouseHover(object sender, EventArgs e)
        {
            previousBTT.BackColor = Color.FromArgb(0, 66, 108);
        }
        private void previousBTT_MouseLeave(object sender, EventArgs e)
        {
            previousBTT.BackColor = Color.FromArgb(209, 222, 230);
        }

        //BTN Multi (S)
        private void MultiBTN_Click(object sender, EventArgs e)
        {
            this.SourView = "Multi.";
            MultiBTN.BorderStyle = BorderStyle.FixedSingle;
            PSRBTN.BorderStyle = BorderStyle.None;
            AdsBTN.BorderStyle = BorderStyle.None;
            AllSBTN.BorderStyle = BorderStyle.None;

            DataBlocksDGV_Act();
        }
        private void MultiBTN_MouseLeave(object sender, EventArgs e)
        {
            MultiBTN.BackColor = Color.FromArgb(209, 222, 230);
        }
        private void MultiBTN_MouseHover(object sender, EventArgs e)
        {

            MultiBTN.BackColor = Color.FromArgb(0, 66, 108);
        }

        //BTN Psr (S)
        private void PSRBTN_Click(object sender, EventArgs e)
        {
            this.SourView = "TYP: SMR";
            MultiBTN.BorderStyle = BorderStyle.None;
            PSRBTN.BorderStyle = BorderStyle.FixedSingle;
            AdsBTN.BorderStyle = BorderStyle.None;
            AllSBTN.BorderStyle = BorderStyle.None;

            DataBlocksDGV_Act();
        }
        private void PSRBTN_MouseHover(object sender, EventArgs e)
        {
            PSRBTN.BackColor = Color.FromArgb(0, 66, 108);
        }
        private void PSRBTN_MouseLeave(object sender, EventArgs e)
        {
            PSRBTN.BackColor = Color.FromArgb(209, 222, 230);
        }

        //BTN Ads (S)
        private void AdsBTN_Click(object sender, EventArgs e)
        {
            this.SourView = "ADS-B";
            MultiBTN.BorderStyle = BorderStyle.None;
            PSRBTN.BorderStyle = BorderStyle.None;
            AdsBTN.BorderStyle = BorderStyle.FixedSingle;
            AllSBTN.BorderStyle = BorderStyle.None;

            DataBlocksDGV_Act();
        }
        private void AdsBTN_MouseHover(object sender, EventArgs e)
        {
            AdsBTN.BackColor = Color.FromArgb(0, 66, 108);
        }
        private void AdsBTN_MouseLeave(object sender, EventArgs e)
        {
            AdsBTN.BackColor = Color.FromArgb(209, 222, 230);
        }

        //BTN All (S)
        private void AllSBTN_Click(object sender, EventArgs e)
        {
            this.SourView = "All";
            MultiBTN.BorderStyle = BorderStyle.None;
            PSRBTN.BorderStyle = BorderStyle.None;
            AdsBTN.BorderStyle = BorderStyle.None;
            AllSBTN.BorderStyle = BorderStyle.FixedSingle;

            DataBlocksDGV_Act();
        }
        private void AllSBTN_MouseLeave(object sender, EventArgs e)
        {
            AllSBTN.BackColor = Color.FromArgb(209, 222, 230);
        }
        private void AllSBTN_MouseHover(object sender, EventArgs e)
        {
            AllSBTN.BackColor = Color.FromArgb(0, 66, 108);
        }

        //Obrir info al DGV DataBlock view
        private void DataBlocksAll_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            DataBlocksAll.CurrentRow.Selected = true;
            this.Cursor = Cursors.WaitCursor;
            string ID_I = DataBlocksAll.Rows[e.RowIndex].Cells["DataBlock Id"].FormattedValue.ToString();
            int i = 0; bool en = false;
            while ((i < DataBlockList.Count())&&(en == false))
            {
                if (DataBlockList[i].ID_Intern == ID_I)
                {
                    DataBlockViwerDGV_Act(DataBlockList[i]);
                    en = true;
                }
                i++;
            }
            this.Cursor = Cursors.Default;
        }

        //BTN Buscar + TextBox Buscar
        private void BuscarBTN_Click(object sender, EventArgs e)
        {
            DataInf.Text = "Loading...";
            DataInf.ForeColor = Color.DarkGray;
            DataInf.Refresh();
            this.Cursor = Cursors.WaitCursor;
            this.IdView = Buscar.Text;
            if (IdView == "")
                this.IdView = "All";
            DataBlocksDGV_Act();
            this.Cursor = Cursors.Default;
            DataInf.Text = "Data loaded";
            DataInf.ForeColor = Color.Green;
        }
        private void BuscarBTN_MouseHover(object sender, EventArgs e)
        {
            BuscarBTN.BackColor = Color.FromArgb(0, 66, 108);
        }
        private void BuscarBTN_MouseLeave(object sender, EventArgs e)
        {
            BuscarBTN.BackColor = Color.FromArgb(209, 222, 230);
        }
        private void Buscar_TextChanged(object sender, EventArgs e)
        {
            if (Buscar.Text == "")
            {
                this.IdView = "All";
                DataBlocksDGV_Act();
            }
        }

        
    }
}
