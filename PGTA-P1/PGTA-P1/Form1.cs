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

namespace PGTA_P1
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            PGB1.Minimum = 1;
            byte[] Bytes = File.ReadAllBytes("201002-lebl-080001_smr_mlat_adsb.ast"); //vector bytes todos juntos, sin separar ni nada
            PGB1.Maximum = Bytes.Count();

            List<DataBlock> DataBlockList = new List<DataBlock>();//lista con paquetes separados

            int i = 0;
            while (i < Bytes.Count())
            {
                //Obtenirm dades inicials del block
                string CAT = Bytes[i].ToString();
                int Long = Convert.ToInt32(Bytes[i + 2].ToString());
                Queue<byte> DataBlock = new Queue<byte>();

                //Introduim tots els bytes dins d'una queue per crear el DataBlock
                int j = 0;
                while (j < Long)
                {
                    DataBlock.Enqueue(Bytes[j + i]); //Afegim a la llista local
                    j++;
                }

                //Si es de la categoria desitjada l'enllistem a la llista general
                if ((CAT == "10") || (CAT == "21"))
                {
                    DataBlockList.Add(new DataBlock(DataBlock, Hertz_Hülsmeyer.CarregarCategories())); //Afegim a la llista general
                }

                i = i + j;
                PGB1.Step = j;
                PGB1.PerformStep();
            }
        }
    }
}
