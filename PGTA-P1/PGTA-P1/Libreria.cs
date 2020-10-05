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

        string Cat;
        byte[] Long = new byte[2];
        string FSPEL = "";
        List<DataField> DataFields = new List<DataField>();

        CatLib ItemsCatInfo;
        string From; //ADS-B, SMR, otro

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="Bytes"></param>
        public DataBlock(Queue<byte> Bytes, CatLib[] Categories)
        {
            if (Bytes.Count != 0)
            {
                this.Original = Bytes.ToList();
                this.Cat = Convert.ToString(Bytes.Dequeue());

                if (Cat == Convert.ToString(Categories[0].Num))
                    ItemsCatInfo = Categories[0];
                else
                    ItemsCatInfo = Categories[1];

                this.Long[0] = Bytes.Dequeue();
                this.Long[1] = Bytes.Dequeue();

                //Proces de set del FSPEL.
                bool FSPEL_Control = true;
                while (FSPEL_Control == true)
                {
                    if (FSPEL_Control == true)
                    {
                        byte New = Bytes.Dequeue();
                        string ByteString = Convert.ToString(New, 2).PadLeft(8, '0');
                        FSPEL = "" + FSPEL + "" + ByteString + ""; //Unim FSPEL

                        //Mirem si l'ultim bit es un 1 o un 0.
                        char[] Bits = ByteString.ToCharArray();
                        if (Bits[7] == '0')
                            FSPEL_Control = false;
                    }
                }

                //Proces de set dels DataFields. Primer de tot hem d'analitzar el FSPEL, despres amb la info extreta dels DataItems creem dataFields.
                char[] Bitss = FSPEL.ToCharArray();
                int bit = 0;
                int oct = 1;
                while (bit < Bitss.Count())
                {
                    if (bit != ((8 * oct) - 1)) //Si el bit no es un FX
                    {
                        char Valor = Bitss[bit]; //Si igual a 1 item present, igual a 0 no present

                        //Recorrido per trobar la info del item-field
                        DataItem Valorant = new DataItem();
                        int ii = 0;
                        bool E = false;
                        while ((ii < ItemsCatInfo.ItemsCat.Count()) && (E == false))
                        {
                            if (ItemsCatInfo.ItemsCat[ii].FRN_B == bit)
                            {
                                Valorant = ItemsCatInfo.ItemsCat[ii];
                                E = true;
                            }
                            ii++;    
                        }

                        if (Valor == '1') //Item present al data field, procedim a guardarho a la nostra llista local
                        {
                            DataField New = new DataField();
                            New.Info = Valorant;

                            int MaxOct = New.Info.Len; //Longitud de les dades del item, 0 variable major de 100 repetitiu. 
                            if (MaxOct > 100)//Repetitiu
                            {
                                byte Evaluat = Bytes.Dequeue();
                                New.Octets.Enqueue(Evaluat); //Afegim al nostra DataField

                                int repeticions = Convert.ToInt32(Evaluat);
                                int i = 0;
                                while (i < repeticions)
                                {
                                    New.Octets.Enqueue(Evaluat); //Afegim al nostra DataField
                                    i++;
                                }
                            }
                            else if (MaxOct == 0)//variable
                            {
                                bool DataFieldB = true;
                                while (DataFieldB == true)
                                {
                                    byte Evaluat = Bytes.Dequeue();
                                    New.Octets.Enqueue(Evaluat); //Afegim al nostra DataField

                                    //Mirem si segueix o no, 0 no segueix 1 si.
                                    string EvaString = Convert.ToString(Evaluat, 2).PadLeft(8, '0');
                                    char[] EvaChar = EvaString.ToCharArray();
                                    if (EvaChar[7] == '0')
                                        DataFieldB = false;
                                }
                            }
                            else//limitat
                            {
                                int i = 0;
                                while (i < MaxOct)
                                {
                                    New.Octets.Enqueue(Bytes.Dequeue());
                                    i++;
                                }
                            }
                            //Deocodifiquem info del data field.
                            New.Decodificar();
                            //Afegim el DataField creat a la nostra llista de datafields
                            DataFields.Add(New);
                        }
                    }
                    else
                    {
                        oct++;
                    }
                        
                    bit++;
                }

                GetFrom();
            }
        }

        /// <summary>
        /// Extreu dels DataFields l'origen de tot el block.
        /// </summary>
        private void GetFrom()
        {
            int c = DataFields.Count();
            int i = 0; bool e = false;
            while((i<c)&&(e==false))
            {
                DataField Evaluat = DataFields[i];
                if(Evaluat.Info.DataItemID[0] == "I010")
                {
                    //Cat 010
                    if (Evaluat.Info.DataItemID[1] == "020")
                    {
                        e = true;
                        this.From = Evaluat.DeCode[0];
                    }
                    i++;
                }
                else 
                {
                    //Cat 021
                    this.From = "ADS-B";
                    e = true;
                }
                    
            }
        }
    }

    /// <summary>
    /// representació d'un data field, decodificacio dels missatges
    /// </summary>
    public class DataField
    {
        public DataItem Info = new DataItem();
        public Queue<byte> Octets = new Queue<byte>();

        public List<string> DeCode = new List<string>();

        public void Decodificar()
        {
            if (Info.DataItemID[0] == "I010")
            {
                //Categoria 010
                if (Info.DataItemID[1] == "000")
                {
                    //Item 000 MessageType
                    int DataOctet = Convert.ToInt32(Octets.Dequeue());
                    if (DataOctet == 1)
                        DeCode.Add("Target Report");
                    else if (DataOctet == 2)
                        DeCode.Add("Start of Update Cycle");
                    else if (DataOctet == 3)
                        DeCode.Add("Periodic Status Message");
                    else if (DataOctet == 4)
                        DeCode.Add("Event-triggered Status Message");
                    else
                        DeCode.Add("ERROR in MessageType");
                }
                else if (Info.DataItemID[1] == "010")
                {
                    //Item 010 Data Source Identifier 
                    int[] DataOctet = new int[2];
                    DataOctet[0] = Convert.ToInt32(Octets.Dequeue());
                    DataOctet[1] = Convert.ToInt32(Octets.Dequeue());
                    string Lin = "SAC = " + DataOctet[0] + ", SIC = " + DataOctet[1] + "";
                    DeCode.Add(Lin);
                }
                else if (Info.DataItemID[1] == "020")
                {
                    //Item 020 Target Report Descriptor
                    string DataOctet = Convert.ToString(Octets.Dequeue(), 2).PadLeft(8, '0');
                    char[] bitsOctet = DataOctet.ToCharArray();

                    //TYP
                    string TYP = "" + bitsOctet[0] + "" + bitsOctet[1] + "" + bitsOctet[2] + "";
                    if (TYP == "000")
                    {
                        DeCode.Add("TYP: SSR multilateration");
                    }
                    else if (TYP == "001")
                    {
                        DeCode.Add("TYP: Mode S multilateration");
                    }
                    else if (TYP == "010")
                    {
                        DeCode.Add("TYP: ADS-B");
                    }
                    else if (TYP == "011")
                    {
                        DeCode.Add("TYP: PSR");
                    }
                    else if (TYP == "100")
                    {
                        DeCode.Add("TYP: Magnetic Loop System");
                    }
                    else if (TYP == "101")
                    {
                        DeCode.Add("TYP: HF multilateration");
                    }
                    else if (TYP == "110")
                    {
                        DeCode.Add("TYP: Not defined");
                    }
                    else
                        DeCode.Add("TYP: Other types");

                    //DCR
                    string DCR = "" + bitsOctet[3] + "";
                    if (DCR == "0")
                        DeCode.Add("DCR: No differential correction (ADS-B)");
                    else
                        DeCode.Add("DCR: Differential correction (ADS-B)");

                    //CHN
                    string CHN = "" + bitsOctet[4] + "";
                    if(CHN == "0")
                        DeCode.Add("CHN: Chain 1");
                    else
                        DeCode.Add("CHN: Chain 2");

                    //GBS 
                    string GBS = "" + bitsOctet[5] + "";
                    if (GBS == "0")
                        DeCode.Add("GBS: Transponder Ground bit not set");
                    else
                        DeCode.Add("GBS: Transponder Ground bit set");

                    //CRT
                    string CRT = "" + bitsOctet[6] + "";
                    if (CRT == "0")
                        DeCode.Add("CRT: No Corrupted reply in multilateration");
                    else
                        DeCode.Add("CRT: Corrupted replies in multilateration");

                    //Primer FX
                    string FX = "" + bitsOctet[7] + "";
                    if (FX == "1")
                    {
                        DataOctet = Convert.ToString(Octets.Dequeue(), 2).PadLeft(8, '0');
                        bitsOctet = DataOctet.ToCharArray();

                        //SIM 
                        string SIM = "" + bitsOctet[0] + "";
                        if (SIM == "0")
                            DeCode.Add("SIM: Actual target report");
                        else
                            DeCode.Add("SIM: Simulated target report");

                        //TST
                        string TST = "" + bitsOctet[1] + "";
                        if (TST == "0")
                            DeCode.Add("TST: Default");
                        else
                            DeCode.Add("TST: Test Target");

                        //RAB
                        string RAB = "" + bitsOctet[2] + "";
                        if (RAB == "0")
                            DeCode.Add("RAB: Report from target transponder");
                        else
                            DeCode.Add("RAB: Report from field monitor(fixed transponder)");

                        //LOP
                        string LOP = "" + bitsOctet[3] + "" + bitsOctet[4] + "";
                        if (RAB == "00")
                            DeCode.Add("LOP: Undetermined");
                        else if(RAB== "01")
                            DeCode.Add("LOP: Loop start");
                        else
                            DeCode.Add("LOP: Loop finish");

                        //LOP
                        string TOT = "" + bitsOctet[5] + "" + bitsOctet[6] + "";
                        if (RAB == "00")
                            DeCode.Add("TOT: Undetermined");
                        else if (RAB == "01")
                            DeCode.Add("TOT: Aircraft");
                        else if (RAB == "10")
                            DeCode.Add("TOT: Ground vehicle");
                        else
                            DeCode.Add("TOT: Helicopter");

                        //Segon FX
                        FX = "" + bitsOctet[7] + "";
                        if (FX == "1")
                        {
                            DataOctet = Convert.ToString(Octets.Dequeue(), 2).PadLeft(8, '0');
                            bitsOctet = DataOctet.ToCharArray();

                            //SPI
                            string SPI = "" + bitsOctet[0] + "";
                            if (SPI == "0")
                                DeCode.Add("SIM: Absence of SPI");
                            else
                                DeCode.Add("SIM: Special Position Identification");
                        }
                    }
                }
                else if (Info.DataItemID[1] == "040")
                {
                    //Item 040 Measured Position in Polar Co-ordinates 
                    byte[] RHO = new byte[2];
                    byte[] Theta = new byte[2];
                    RHO[1] = Octets.Dequeue();
                    RHO[0] = Octets.Dequeue();
                    Theta[1] = Octets.Dequeue();
                    Theta[0] = Octets.Dequeue();

                    int RHO_Dec = BitConverter.ToInt16(RHO, 0);
                    int Theta_Dec_BIN = BitConverter.ToInt16(Theta, 0);
                    double Theta_Dec_Grad = Theta_Dec_BIN * 0.0055;

                    DeCode.Add(Convert.ToString(RHO_Dec));
                    DeCode.Add(Convert.ToString(Theta_Dec_Grad));
                }
                else if (Info.DataItemID[1] == "041")
                {
                    //Item 041 Position in WGS-84 Co-ordinates  
                }
                else if (Info.DataItemID[1] == "042")
                {
                    //Item 042 Position in Cartesian Co-ordinates  
                }
                else if (Info.DataItemID[1] == "060")
                {
                    //Item 060 Mode-3/A Code in Octal Representation 
                }
                else if (Info.DataItemID[1] == "090")
                {
                    //Item 090, Flight Level in Binary Representation 
                }
                else if (Info.DataItemID[1] == "091")
                {
                    //Item 091, Measured Height
                }
                else if (Info.DataItemID[1] == "131")
                {
                    //Item 131, Amplitude of Primary Plot
                }
                else if (Info.DataItemID[1] == "140")
                {
                    //Item 140, Time of Day 
                }
                else if (Info.DataItemID[1] == "161")
                {
                    //Item 161, Track Number 
                }
                else if (Info.DataItemID[1] == "170")
                {
                    //Item 170, Track Status 
                }
                else if (Info.DataItemID[1] == "200")
                {
                    //Item 200, Calculated Track Velocity in Polar Co-ordinates
                }
                else if (Info.DataItemID[1] == "202")
                {
                    //Item 202, Calculated Track Velocity in Cartesian Co-ordinates 
                }
                else if (Info.DataItemID[1] == "210")
                {
                    //Item 210, Calculated Acceleration 
                }
                else if (Info.DataItemID[1] == "220")
                {
                    //Item 220, Target Address
                }
                else if (Info.DataItemID[1] == "245")
                {
                    //Item 245, Target Identification
                }
                else if (Info.DataItemID[1] == "250")
                {
                    //Item 250, Mode S MB Data
                }
                else if (Info.DataItemID[1] == "270")
                {
                    //Item 270, Target Size & Orientation
                }
                else if (Info.DataItemID[1] == "280")
                {
                    //Item 280, Presence
                }
                else if (Info.DataItemID[1] == "300")
                {
                    //Item 300, Vehicle Fleet Identification 
                }
                else if (Info.DataItemID[1] == "310")
                {
                    //Item 310, Pre-programmed Message 
                }
                else if (Info.DataItemID[1] == "500")
                {
                    //Item 500, Standard Deviation of Position 
                }
                else
                {
                    //Intem no inscrit #ERROR#
                }
            }
            else
            {
                //Categoria 021
            }
        }
    }

    /// <summary>
    /// Representació d'un data item (caràcter identificatiu)
    /// </summary>
    public class DataItem
    {
        public int FRN;
        public int FRN_B; //index en el byte
        public string[] DataItemID;
        public string Nom;
        public int Len; // 1+ = 0; 1+2n o 1+8n = 102 o 108

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
            DataItemID = Lin[2].Split('/');
            Nom = Lin[3];
            Len = Convert.ToInt32(Lin[4]);
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
    public class Hertz_Hülsmeyer
    {
        /// <summary>
        /// Retorna una llista de DataBlocks extrets de l'arxiu proporcionat.
        /// </summary>
        /// <param name="Input"></param> Nom arxiu .bin .ast
        /// <returns></returns>
        public static List<DataBlock> DecodificarDataBlocksV1(string Input, CatLib[] Categories)
        {
            byte[] Bytes = File.ReadAllBytes(Input); //vector bytes todos juntos, sin separar ni nada
            
            List<DataBlock> DataBlockList = new List<DataBlock>();//lista con paquetes separados

            /*
             * Buscarem les capcaleres de les categories (10 o 21), allà començarem una llista amb tots els bytes d'un paquet.
             * Quan tornem a trobar un capçal de categoria la llista es tancara i s'afagira a la llista general. Repetirem el procediment.
             */
            Queue<byte> DataBlock = new Queue<byte>();

            int i = 0;

            while (i < Bytes.Count())
            {
                string ByteString = Convert.ToString(Bytes[i], 2).PadLeft(8, '0');
                if ((ByteString == "00001010")||(ByteString == "00010101")) //Primer condició cat10, segona cat21
                {
                    //Mai tindrem una longitud inferior a 3, aqui ho comprovem. També mirem que el FSPEL sigui de com a minim 8.
                    if ((Bytes[i + 1] == Convert.ToByte(0)) && (Bytes[i + 2] > Convert.ToByte(3)) && Bytes[i+3] >= Convert.ToByte(8)) 
                    {
                        DataBlockList.Add(new DataBlock(DataBlock,Categories)); //Afegim a la llista general
                        DataBlock = new Queue<byte>(); //Reset
                    }

                    
                }
                DataBlock.Enqueue(Bytes[i]); //Afegim a la llista local
                i++;
            }
            DataBlockList.Add(new DataBlock(DataBlock,Categories));
            DataBlockList.RemoveAt(0); //Eliminar primera posició ja que es nula.

            return DataBlockList;
        }

        /// <summary>
        /// Retorna una llista de DataBlocks extrets de l'arxiu proporcionat.
        /// </summary>
        /// <param name="Input"></param> Nom arxiu .bin .ast
        /// <returns></returns>
        public static List<DataBlock> DecodificarDataBlocksV2(string Input, CatLib[] Categories)
        {
            byte[] Bytes = File.ReadAllBytes(Input); //vector bytes todos juntos, sin separar ni nada
            List<DataBlock> DataBlockList = new List<DataBlock>();//lista con paquetes separados

            int i = 0;
            while (i < Bytes.Count())
            {
                //Obtenirm dades inicials del block
                string CAT = Bytes[i].ToString();
                int Long = Convert.ToInt32(Bytes[i+2].ToString());
                Queue<byte> DataBlock = new Queue<byte>();

                //Introduim tots els bytes dins d'una queue per crear el DataBlock
                int j = 0;
                while (j < Long)
                {
                    DataBlock.Enqueue(Bytes[j+i]); //Afegim a la llista local
                    j++;
                }

                //Si es de la categoria desitjada l'enllistem a la llista general
                if((CAT == "10")||(CAT == "21"))
                {
                    DataBlockList.Add(new DataBlock(DataBlock, Categories)); //Afegim a la llista general
                    DataBlock = new Queue<byte>();
                }

                i = i + j;
            }

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
                    if (New.DataItemID[0] == "I010")
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
        public static void test()
        {
            //DataBlock Test = new DataBlock(Data[0]);
            //int A = 0;
            //byte[] A = new byte[2];
            //A[1] = Convert.ToByte("00000001");
            //A[0] = Convert.ToByte(0);

            //int Fin = BitConverter.ToInt16(A, 0);
            //

            byte[] RHO = new byte[8];
            byte[] Theta = new byte[2];
            RHO[6] = Convert.ToByte(15);
            RHO[7] = Convert.ToByte(55);
            //Theta[1] = Octets.Dequeue();
            //Theta[0] = Octets.Dequeue();

            double RHO_Dec = BitConverter.ToDouble(RHO,6);
            double Theta_Dec = BitConverter.ToDouble(Theta, 0);
        }
    }

    
}
