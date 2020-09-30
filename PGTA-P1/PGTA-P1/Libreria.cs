using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PGTA_P1
{
    /// <summary>
    /// Representació d'un data block
    /// </summary>
    public class DataBlock
    {
        List<byte> Original = new List<byte>();

        byte Cat = new byte();
        byte[] Long = new byte[2];
        List<byte> FSPEL = new List<byte>();
        List<byte> DataField = new List<byte>();

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="Bytes"></param>
        public DataBlock(List<byte> Bytes)
        {
            if (Bytes.Count != 0)
            {
                this.Original = Bytes;
                this.Cat = Bytes[0];
                this.Long[0] = Bytes[1];
                this.Long[1] = Bytes[2];

                int i = 3; //Comencem a 3 ja que els tres primers index ja s'han assignat.
                bool FSPEL_Control = true;
                while (i < Bytes.Count())
                {
                    if (FSPEL_Control == true)
                    {
                        FSPEL.Add(Bytes[i]);

                        //Mirem si l'ultim bit es un 1 o un 0.
                        string ByteString = Convert.ToString(Bytes[i], 2).PadLeft(8, '0');
                        char[] Bits = ByteString.ToCharArray();
                        if (Bits[7] == '0')
                            FSPEL_Control = false;
                    }
                    else
                        DataField.Add(Bytes[i]);

                    i++;
                }
            }
        }
    }

    /// <summary>
    /// representació d'un data field
    /// </summary>
    public class DataField
    {
        DataItem info = new DataItem();
        List<byte> Octets = new List<byte>();
    }

    /// <summary>
    /// Representació d'un data item (caràcter identificatiu)
    /// </summary>
    public class DataItem
    {
        int FRN;
        int FRN_B; //index en el byte
        string DataItemID;
        string Nom;
        int Len; // 1+ = 0; 1+2n o 1+8n = 102 o 108

        string From; //ADS-B, SMR, otro (set getCat)

        public DataItem()
        { }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="Lin"></param>
        public DataItem(string[] Lin)
        {
            FRN = Convert.ToInt32(Lin[0]);
            FRN_B = Convert.ToInt32(Lin[1]);
            DataItemID = Lin[2];
            Nom = Lin[3];
            Len = Convert.ToInt32(Lin[4]);
        }

        /// <summary>
        /// Retorna la categoria del dataItem
        /// </summary>
        /// <returns></returns>
        public string GetCat()
        {
            if (DataItemID.Split('/')[0] == "I021")
                From = "ADS-B";
            return DataItemID.Split('/')[0];
        }
    }

    /// <summary>
    /// Representació d'una categoria (conté els diferents data items de cada cat)
    /// </summary>
    public class CatLib
    {
        public int Num;
        public List<DataItem> ItemsCat = new List<DataItem>();

        public CatLib(int A)
        {
            Num = A;
        }
    }

    /// <summary>
    /// Seqüéncia de decodificació.
    /// </summary>
    public class DeCod
    {
        /// <summary>
        /// Retorna una llista de DataBlocks extrets de l'arxiu proporcionat.
        /// </summary>
        /// <param name="Input"></param> Nom arxiu .bin .ast
        /// <returns></returns>
        public static List<DataBlock> DecodificarDataBlocks(string Input)
        {
            byte[] Bytes = File.ReadAllBytes(Input); //vector bytes todos juntos, sin separar ni nada
            
            List<DataBlock> DataBlockList = new List<DataBlock>();//lista con paquetes separados

            /*
             * Buscarem les capcaleres de les categories (10 o 21), allà començarem una llista amb tots els bytes d'un paquet.
             * Quan tornem a trobar un capçal de categoria la llista es tancara i s'afagira a la llista general. Repetirem el procediment.
             */
            List<byte> DataBlock = new List<byte>();

            int i = 0;

            while (i < Bytes.Count())
            {
                string ByteString = Convert.ToString(Bytes[i], 2).PadLeft(8, '0');
                if ((ByteString == "00001010")|| (ByteString == "00010101")) //Primer condició cat10, segona cat21
                {
                    //Mai tindrem una longitud inferior a 3, aqui ho comprovem. També mirem que el FSPEL sigui de com a minim 8.
                    if ((Bytes[i + 1] == Convert.ToByte(0)) && (Bytes[i + 2] > Convert.ToByte(3)) && Bytes[i+3] >= Convert.ToByte(8)) 
                    {
                        DataBlockList.Add(new DataBlock(DataBlock)); //Afegim a la llista general
                        DataBlock = new List<byte>(); //Reset
                    }
                }

                DataBlock.Add(Bytes[i]); //Afegim a la llista local
                i++;
            }
            DataBlockList.Add(new DataBlock(DataBlock));
            DataBlockList.RemoveAt(0); //Eliminar primera posició ja que es nula.

            return DataBlockList;
        }

        /// <summary>
        /// Carrega la info de les categories amb els seus Items i ho retorna en un vector.
        /// </summary>
        /// <returns></returns>
        public static CatLib[] CarregarCategories()
        {
            try
            {
                CatLib[] Categories = new CatLib[2];
                Categories[0] = new CatLib(10);
                Categories[1] = new CatLib(21);

                StreamReader R = new StreamReader("DataItems.txt");
                int Max = Convert.ToInt32(R.ReadLine());
                int i = 0;
                while (i < Max)
                {
                    string A = R.ReadLine();
                    string[] Lin = A.Split('_');
                    DataItem New = new DataItem(Lin);
                    if (New.GetCat() == "I010")
                        Categories[0].ItemsCat.Add(New);
                    else
                        Categories[1].ItemsCat.Add(New);
                    i++;
                }

                return Categories;
            }
            catch (FormatException)
            {
                return null;
            }
            catch (FileNotFoundException)
            {
                return null;
            }
        }

        /// <summary>
        /// Test
        /// </summary>
        /// <param name="Data"></param>
        public static void test(List<List<byte>> Data)
        {
            DataBlock Test = new DataBlock(Data[0]);
            int A = 0;
            //byte[] A = new byte[2];
            //A[1] = Convert.ToByte("00000001");
            //A[0] = Convert.ToByte(0);

            //int Fin = BitConverter.ToInt16(A, 0);
            //
        }
    }

    
}
