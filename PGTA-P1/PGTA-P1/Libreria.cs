using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace PGTA_P1
{
    //Representació del DataBlock
    public class DataBlock
    {
        List<byte> Original = new List<byte>();

        string Cat;
        byte[] Long = new byte[2];
        string FSPEL = "";
        List<DataField> DataFields = new List<DataField>();

        CatLib ItemsCatInfo;
        string From; //ADS-B, SMR, otro

        //A part de construir el DataBlock s'encarrega de repartir la info binaria en cada DataField
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

        //A partir del DataField adecuat obté l'origen del DataBloc
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

    //Representació del DataField
    public class DataField
    {
        public DataItem Info = new DataItem();
        public Queue<byte> Octets = new Queue<byte>();

        public List<string> DeCode = new List<string>();

        //Decodifica el DataField a partir de la cadena de octets rebuda. 
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
                    if (CHN == "0")
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
                        else if (RAB == "01")
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

                    this.Info.units.Add("m");
                    this.Info.units.Add("º");
                }
                else if (Info.DataItemID[1] == "041")
                {
                    //Item 041 Position in WGS-84 Co-ordinates  
                    byte[] Lat = new byte[4];
                    byte[] Lon = new byte[4];
                    Lat[3] = Octets.Dequeue();
                    Lat[2] = Octets.Dequeue();
                    Lat[1] = Octets.Dequeue();
                    Lat[0] = Octets.Dequeue();
                    Lon[3] = Octets.Dequeue();
                    Lon[2] = Octets.Dequeue();
                    Lon[1] = Octets.Dequeue();
                    Lon[0] = Octets.Dequeue();

                    string LatS = "" + Convert.ToString(Lat[3], 2).PadLeft(8, '0') + "" + Convert.ToString(Lat[2], 2).PadLeft(8, '0') + "" + Convert.ToString(Lat[1], 2).PadLeft(8, '0') + "" + Convert.ToString(Lat[0], 2).PadLeft(8, '0') + "";
                    char[] LatC = LatS.ToCharArray();
                    string LonS = "" + Convert.ToString(Lon[3], 2).PadLeft(8, '0') + "" + Convert.ToString(Lon[2], 2).PadLeft(8, '0') + "" + Convert.ToString(Lon[1], 2).PadLeft(8, '0') + "" + Convert.ToString(Lon[0], 2).PadLeft(8, '0') + "";
                    char[] LonC = LonS.ToCharArray();

                    ////LAT
                    //if (LatC[0] == '0') //Positiu
                    //{
                    //    int ByteInt = BitConverter.ToInt32(Lat, 0);
                    //    double LatD = ByteInt * (180 / 2 ^ 31);
                    //    DeCode.Add(Convert.ToString(LatD));
                    //}
                    //else //Negatiu
                    //{
                    //    //Transformem el rastre string de 0 a 1
                    //    int i = 0;
                    //    while (i < LatC.Length)
                    //    {
                    //        if (LatC[i] == '1')
                    //            LatC[i] = '0';
                    //        else
                    //            LatC[i] = '1';
                    //        i++;
                    //    }

                    //    //Obtenirm el numero Binari-Decimal per fer les operacions
                    //    i = 0; int BY = 3;
                    //    while (i < LatC.Length)
                    //    {
                    //        char[] NewString = new char[8];
                    //        int j = 0;
                    //        while (j < 7)
                    //        {
                    //            NewString[j] = LatC[i+j];
                    //            j++;
                    //        }
                    //        Lat[BY] = Convert.ToByte(NewString.ToString());
                    //        i = i + j;
                    //        BY--;
                    //    }

                    //    int ByteInt = BitConverter.ToInt32(Lat, 0);
                    //    double LatD = -1*ByteInt * (180 / 2 ^ 31);
                    //    DeCode.Add(Convert.ToString(LatD));
                    //}

                    ////LON
                    //if (LonC[0] == '0') //Positiu
                    //{
                    //    int ByteInt = BitConverter.ToInt32(Lon, 0);
                    //    double LonD = ByteInt * (180 / 2 ^ 31);
                    //    DeCode.Add(Convert.ToString(LonD));
                    //}
                    //else //Negatiu
                    //{
                    //    //Transformem el rastre string de 0 a 1
                    //    int i = 0;
                    //    while (i < LonC.Length)
                    //    {
                    //        if (LonC[i] == '1')
                    //            LonC[i] = '0';
                    //        else
                    //            LonC[i] = '1';
                    //        i++;
                    //    }

                    //    //Obtenirm el numero Binari-Decimal per fer les operacions
                    //    i = 0; int BY = 3;
                    //    while (i < LonC.Length)
                    //    {
                    //        char[] NewString = new char[8];
                    //        int j = 0;
                    //        while (j < 7)
                    //        {
                    //            NewString[j] = LonC[i + j];
                    //            j++;
                    //        }
                    //        Lon[BY] = Convert.ToByte(NewString.ToString());
                    //        i = i + j;
                    //        BY--;
                    //    }

                    //    int ByteInt = BitConverter.ToInt32(Lon, 0);
                    //    double LonD = -1*ByteInt * (180 / 2 ^ 31);
                    //    DeCode.Add(Convert.ToString(LonD));
                    //}
                } //NO TEST (BAD)
                else if (Info.DataItemID[1] == "042")
                {
                    //Item 042 Position in Cartesian Co-ordinates  
                    byte[] Xc = new byte[2];
                    byte[] Yc = new byte[2];
                    Xc[1] = Octets.Dequeue();
                    Xc[0] = Octets.Dequeue();
                    Yc[1] = Octets.Dequeue();
                    Yc[0] = Octets.Dequeue();

                    int ByteInt = BitConverter.ToInt16(Xc, 0);
                    DeCode.Add(Convert.ToString(ByteInt));
                    ByteInt = BitConverter.ToInt16(Yc, 0);
                    DeCode.Add(Convert.ToString(ByteInt));

                    this.Info.units.Add("m");
                    this.Info.units.Add("m");
                } 
                else if (Info.DataItemID[1] == "060")
                {
                    //Item 060 Mode-3/A Code in Octal Representation 
                    string DataOctet = "" + Convert.ToString(Octets.Dequeue(), 2).PadLeft(8, '0') + "" + Convert.ToString(Octets.Dequeue(), 2).PadLeft(8, '0') + "";
                    char[] bitsOctet = DataOctet.ToCharArray();

                    if (bitsOctet[0] == '0')
                        DeCode.Add("V: Code validated");
                    else
                        DeCode.Add("V: Code not validated");

                    if (bitsOctet[1] == '0')
                        DeCode.Add("G: Default");
                    else
                        DeCode.Add("G: Garbled code");

                    if (bitsOctet[2] == '0')
                        DeCode.Add("L: Mode-3/A code derived from the reply of the transponder");
                    else
                        DeCode.Add("L: Mode-3/A code not extracted during the last scan");

                    int i = 4;
                    char[] Reply = new char[12];
                    while (i < bitsOctet.Length)
                    {
                        Reply[i - 4] = bitsOctet[i];
                        i++;
                    }
                    DeCode.Add(Reply.ToString());
                }
                else if (Info.DataItemID[1] == "090")
                {
                    //Item 090, Flight Level in Binary Representation 
                    string DataOctet = "" + Convert.ToString(Octets.Dequeue(), 2).PadLeft(8, '0') + "" + Convert.ToString(Octets.Dequeue(), 2).PadLeft(8, '0') + "";
                    char[] bitsOctet = DataOctet.ToCharArray();

                    if (bitsOctet[0] == '0')
                        DeCode.Add("V: Code validated");
                    else
                        DeCode.Add("V: Code not validated");

                    if (bitsOctet[1] == '0')
                        DeCode.Add("G: Default");
                    else
                        DeCode.Add("G: Garbled code");

                    int i = 2;
                    char[] BitSuperior = new char[8];
                    BitSuperior[0] = '0';
                    BitSuperior[1] = '0';
                    while (i < 8)
                    {
                        BitSuperior[i] = bitsOctet[i];
                        i++;
                    }
                    int j = 0;
                    char[] BitInferior = new char[8];
                    while (j < 8)
                    {
                        BitInferior[j] = bitsOctet[i];
                        i++; j++;
                    }

                    byte[] bitsOctetB = new byte[2];
                    bitsOctetB[1] = Convert.ToByte(new string(BitSuperior), 2);
                    bitsOctetB[0] = Convert.ToByte(new string(BitInferior), 2);
                    int FL = BitConverter.ToInt16(bitsOctetB, 0);
                    double FlFin = FL * 1 / 4;
                    DeCode.Add(Convert.ToString(FlFin));

                    this.Info.units.Add("FL");
                }
                else if (Info.DataItemID[1] == "091")
                {
                    //Item 091, Measured Height
                    byte[] Hgh = new byte[2];
                    Hgh[1] = Octets.Dequeue();
                    Hgh[0] = Octets.Dequeue();

                    int Hgh_Dec = BitConverter.ToInt16(Hgh, 0);
                    double Hgh_ft = Hgh_Dec * 6.25;
                    DeCode.Add(Convert.ToString(Hgh_ft));

                    this.Info.units.Add("FL");
                } //NO TEST
                else if (Info.DataItemID[1] == "131")
                {
                    //Item 131, Amplitude of Primary Plot
                    int PAM = Convert.ToInt32(Octets.Dequeue());
                    DeCode.Add(Convert.ToString(PAM));

                } //NO TEST
                else if (Info.DataItemID[1] == "140")
                {
                    //Item 140, Time of Day 
                    byte[] Time = new byte[4];
                    Time[3] = 0;
                    Time[2] = Octets.Dequeue();
                    Time[1] = Octets.Dequeue();
                    Time[0] = Octets.Dequeue();

                    int Time_Dec = BitConverter.ToInt32(Time, 0);
                    double Time_S = Convert.ToDouble(Time_Dec) / 128;
                    DeCode.Add(Convert.ToString(Time_S));

                    this.Info.units.Add("s (UTC, form midnight)");
                }
                else if (Info.DataItemID[1] == "161")
                {
                    //Item 161, Track Number 
                    byte[] TN = new byte[2];
                    TN[1] = Octets.Dequeue();
                    TN[0] = Octets.Dequeue();

                    int TN_Dec = BitConverter.ToInt16(TN, 0);
                    DeCode.Add(Convert.ToString(TN_Dec));
                } 
                else if (Info.DataItemID[1] == "170")
                {
                    //Item 170, Track Status 
                    string DataOctet = Convert.ToString(Octets.Dequeue(), 2).PadLeft(8, '0');
                    char[] bitsOctet = DataOctet.ToCharArray();

                    //CNF
                    if (bitsOctet[0] == '0')
                        DeCode.Add("CNF: Confirmed track");
                    else
                        DeCode.Add("CNF: Track in initialisation phase");

                    //TRE
                    if (bitsOctet[1] == '0')
                        DeCode.Add("TRE: Default");
                    else
                        DeCode.Add("TRE: Last report for a track");

                    //CST
                    string CST = "" + bitsOctet[2] + "" + bitsOctet[3] + "";
                    if (CST == "00")
                        DeCode.Add("CST: No extrapolation");
                    else if (CST == "01")
                        DeCode.Add("CST: Predictable extrapolation due to sensor refresh period");
                    else if (CST == "10")
                        DeCode.Add("CST: Predictable extrapolation in masked area");
                    else
                        DeCode.Add("CST: Extrapolation due to unpredictable absence of detection");

                    //MAH
                    if (bitsOctet[4] == '0')
                        DeCode.Add("MAH: Default");
                    else
                        DeCode.Add("MAH: Horizontal manoeuvre");

                    //TCC
                    if (bitsOctet[5] == '0')
                        DeCode.Add("TCC: Tracking performed in 'Sensor Plane', i.e. neither slant range correction nor projection was applied.");
                    else
                        DeCode.Add("TCC: Slant range correction and a suitable projection technique are used to track in a 2D.reference plane, tangential to the earth model at the Sensor Site co - ordinates.");

                    //STH 
                    if (bitsOctet[6] == '0')
                        DeCode.Add("STH: Measured position");
                    else
                        DeCode.Add("STH: Smoothed position");

                    //FX
                    string FX = "" + bitsOctet[7] + "";
                    if (FX == "1")
                    {
                        DataOctet = Convert.ToString(Octets.Dequeue(), 2).PadLeft(8, '0');
                        bitsOctet = DataOctet.ToCharArray();

                        //TOM
                        string TOM = "" + bitsOctet[0] + "" + bitsOctet[1] + "";
                        if (TOM == "00")
                            DeCode.Add("TOM: Unknown type of movement");
                        else if (TOM == "01")
                            DeCode.Add("TOM: Taking-off");
                        else if (TOM == "10")
                            DeCode.Add("TOM: Landing");
                        else
                            DeCode.Add("TOM: Other types of movement");

                        //DOU
                        string DOU = "" + bitsOctet[2] + "" + bitsOctet[3] + "" + bitsOctet[4] + "";
                        if (DOU == "000")
                            DeCode.Add("DOU: No doubt");
                        else if (DOU == "001")
                            DeCode.Add("DOU: Doubtful correlation (undetermined reason)");
                        else if (DOU == "010")
                            DeCode.Add("DOU: Doubtful correlation in clutter");
                        else if (DOU == "011")
                            DeCode.Add("DOU: Loss of accuracy");
                        else if (DOU == "100")
                            DeCode.Add("DOU: Loss of accuracy in clutter");
                        else if (DOU == "101")
                            DeCode.Add("DOU: Unstable track");
                        else if (DOU == "101")
                            DeCode.Add("DOU: Previously coasted");

                        //MRS
                        string MRS = "" + bitsOctet[5] + "" + bitsOctet[6] + "";
                        if (MRS == "00")
                            DeCode.Add("MRS: Merge or split indication undetermined");
                        else if (MRS == "01")
                            DeCode.Add("MRS: Track merged by association to plot");
                        else if (MRS == "10")
                            DeCode.Add("MRS: Track merged by non-association to plot");
                        else
                            DeCode.Add("MRS: Split track");

                        //FX
                        FX = "" + bitsOctet[7] + "";
                        if (FX == "1")
                        {
                            DataOctet = Convert.ToString(Octets.Dequeue(), 2).PadLeft(8, '0');
                            bitsOctet = DataOctet.ToCharArray();

                            //GHO
                            if(bitsOctet[0] == 0)
                                DeCode.Add("GHO: Default");
                            else
                                DeCode.Add("GHO: Ghost track");
                        }
                    }
                }
                else if (Info.DataItemID[1] == "200")
                {
                    //Item 200, Calculated Track Velocity in Polar Co-ordinates
                    byte[] Gs = new byte[2];
                    byte[] Ta = new byte[4];
                    Gs[1] = Octets.Dequeue();
                    Gs[0] = Octets.Dequeue();
                    Ta[3] = 0;
                    Ta[2] = 0;
                    Ta[1] = Octets.Dequeue();
                    Ta[0] = Octets.Dequeue();

                    int Gs_Dec = BitConverter.ToInt16(Gs, 0);
                    double Gs_Kt = Gs_Dec * 0.22;
                    DeCode.Add(Convert.ToString(Gs_Dec));

                    int Ta_Dec = BitConverter.ToInt32(Ta, 0);
                    double Ta_Grad = Ta_Dec * 0.0055;
                    DeCode.Add(Convert.ToString(Ta_Grad));

                    this.Info.units.Add("kt");
                    this.Info.units.Add("º");
                }
                else if (Info.DataItemID[1] == "202")
                {
                    //Item 202, Calculated Track Velocity in Cartesian Co-ordinates 
                    byte[] Xc = new byte[2];
                    byte[] Yc = new byte[2];
                    Xc[1] = Octets.Dequeue();
                    Xc[0] = Octets.Dequeue();
                    Yc[1] = Octets.Dequeue();
                    Yc[0] = Octets.Dequeue();

                    int ByteInt = BitConverter.ToInt16(Xc, 0);
                    DeCode.Add(Convert.ToString(ByteInt*0.25));
                    ByteInt = BitConverter.ToInt16(Yc, 0);
                    DeCode.Add(Convert.ToString(ByteInt*0.25));

                    this.Info.units.Add("m/s");
                    this.Info.units.Add("m/s");
                }
                else if (Info.DataItemID[1] == "210")
                {
                    //Item 210, Calculated Acceleration 
                    byte Ax = Octets.Dequeue();
                    byte Ay = Octets.Dequeue();

                    int Ax_Dec;
                    if (Ax > 255 / 2)
                    {
                        Ax_Dec = -1 * (255 + 1) + Ax;
                    }
                    else
                        Ax_Dec = Ax;
                    double Ax_ms = Ax_Dec * 0.25;
                    DeCode.Add(Convert.ToString(Ax_ms));

                    int Ay_Dec;
                    if (Ay > 255 / 2)
                    {
                        Ay_Dec = -1 * (255 + 1) + Ay;
                    }
                    else
                        Ay_Dec = Ay;
                    double Ay_ms = Ay_Dec * 0.25;
                    DeCode.Add(Convert.ToString(Ay_ms));

                    this.Info.units.Add("m/s^2");
                    this.Info.units.Add("m/s^2");
                }
                else if (Info.DataItemID[1] == "220")
                {
                    //Item 220, Target Address
                    byte[] TA = new byte[4];
                    TA[3] = 0;
                    TA[2] = Octets.Dequeue();
                    TA[1] = Octets.Dequeue();
                    TA[0] = Octets.Dequeue();

                    int TA_Dec = BitConverter.ToInt32(TA, 0);
                    DeCode.Add(Convert.ToString(TA_Dec));
                }
                else if (Info.DataItemID[1] == "245")
                {
                    //Item 245, Target Identification
                    string DataOctet = Convert.ToString(Octets.Dequeue(), 2).PadLeft(8, '0');
                    char[] bitsOctet = DataOctet.ToCharArray();

                    //STI
                    string STI = "" + bitsOctet[0] + "" + bitsOctet[1] + "";
                    if (STI == "00")
                        DeCode.Add("STI: Callsign or registration downlinked from transponder");
                    else if (STI == "01")
                        DeCode.Add("STI: Callsign not downlinked from transponder ");
                    else
                        DeCode.Add("STI: Callsign not downlinked from transponder ");

                    //TarID
                    char[] TarID = new char[8];
                    DataOctet = "" + Convert.ToString(Octets.Dequeue(), 2).PadLeft(8, '0') + "" + Convert.ToString(Octets.Dequeue(), 2).PadLeft(8, '0') + "" + Convert.ToString(Octets.Dequeue(), 2).PadLeft(8, '0') + "" + Convert.ToString(Octets.Dequeue(), 2).PadLeft(8, '0') + "" + Convert.ToString(Octets.Dequeue(), 2).PadLeft(8, '0') + "" + Convert.ToString(Octets.Dequeue(), 2).PadLeft(8, '0') + "";
                    bitsOctet = DataOctet.ToCharArray();

                    int i = 0; int h = 0;int k = 1;
                    while (i < 8)
                    {
                        byte b65 = Convert.ToByte("" + bitsOctet[h] + "" + bitsOctet[h + 1] + "", 2);
                        byte b4321 = Convert.ToByte("" + bitsOctet[h + 2] + "" + bitsOctet[h + 3] + "" + bitsOctet[h + 4] + "" + bitsOctet[h + 5] + "", 2);

                        //F1: Num o Ll
                        if (b65 == 3)
                        {
                            //Num
                            string b = Convert.ToString(b4321);
                            TarID[i] = Convert.ToChar(b);
                        }
                        else if (b65 == 2)
                        {
                            char[] Save = TarID;
                            TarID = new Char[8-k];
                            int y = 0;
                            while (y < TarID.Length)
                            {
                                TarID[y] = Save[y];
                                y++;
                            }
                            k++;
                        }
                        else
                        {
                            //Ll
                            if (b65 == 0)
                            {
                                if (b4321 == 1)
                                    TarID[i] = 'A';
                                else if (b4321 == 2)
                                    TarID[i] = 'B';
                                else if (b4321 == 3)
                                    TarID[i] = 'C';
                                else if (b4321 == 4)
                                    TarID[i] = 'D';
                                else if (b4321 == 5)
                                    TarID[i] = 'E';
                                else if (b4321 == 6)
                                    TarID[i] = 'F';
                                else if (b4321 == 7)
                                    TarID[i] = 'G';
                                else if (b4321 == 8)
                                    TarID[i] = 'H';
                                else if (b4321 == 9)
                                    TarID[i] = 'I';
                                else if (b4321 == 10)
                                    TarID[i] = 'J';
                                else if (b4321 == 11)
                                    TarID[i] = 'K';
                                else if (b4321 == 12)
                                    TarID[i] = 'L';
                                else if (b4321 == 13)
                                    TarID[i] = 'M';
                                else if (b4321 == 14)
                                    TarID[i] = 'N';
                                else if (b4321 == 15)
                                    TarID[i] = '0';
                            }
                            else
                            {
                                if (b4321 == 0)
                                    TarID[i] = 'P';
                                else if (b4321 == 1)
                                    TarID[i] = 'Q';
                                else if (b4321 == 2)
                                    TarID[i] = 'R';
                                else if (b4321 == 3)
                                    TarID[i] = 'S';
                                else if (b4321 == 4)
                                    TarID[i] = 'T';
                                else if (b4321 == 5)
                                    TarID[i] = 'U';
                                else if (b4321 == 6)
                                    TarID[i] = 'V';
                                else if (b4321 == 7)
                                    TarID[i] = 'W';
                                else if (b4321 == 8)
                                    TarID[i] = 'X';
                                else if (b4321 == 9)
                                    TarID[i] = 'Y';
                                else if (b4321 == 10)
                                    TarID[i] = 'Z';
                            }
                        }
                        i++;
                        h = h + 6;
                    }
                    DeCode.Add(new string(TarID));
                }
                else if (Info.DataItemID[1] == "250")
                {
                    //Item 250, Mode S MB Data
                    int Rep = Octets.Dequeue();
                    string DataOctet = "";
                    int i = 0;
                    while (i < Rep-1)
                    {
                        DataOctet = ""+DataOctet+""+ Convert.ToString(Octets.Dequeue(), 2).PadLeft(8, '0') + "";
                        i++;
                    }

                    string BDS = Convert.ToString(Octets.Dequeue(), 2).PadLeft(8, '0');
                    char[] bitsOctet = DataOctet.ToCharArray();
                    char[] BDS1 = new char[4]; char[] BDS2 = new char[4];
                    i = 0;
                    while (i < 4)
                    {
                        BDS1[i] = bitsOctet[i];
                        BDS2[i] = bitsOctet[i+4];
                        i++;
                    }
                    DeCode.Add(DataOctet);
                    DeCode.Add(new string(BDS1));
                    DeCode.Add(new string(BDS2));
                } //NO TEST
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

                //Item 008, Aircraft Operational Status
                if (Info.DataItemID[1] == "008")
                {

                    string DataOctet = Convert.ToString(Octets.Dequeue(), 2).PadLeft(8, '0');
                    char[] bitsOctet = DataOctet.ToCharArray();

                    //RA
                    if (bitsOctet[0] == '0')
                        DeCode.Add("RA: TCAS II or ACAS RA not active");
                    else
                        DeCode.Add("RA: TCAS RA active");

                    //TC
                    string TC = "" + bitsOctet[1] + "" + bitsOctet[2] + "";
                    if (TC == "00")
                        DeCode.Add("TC: no capability for Trajectory Change Reports");
                    else if (TC == "01")
                        DeCode.Add("TC: support for TC+0 reports only");
                    else if (TC == "10")
                        DeCode.Add("TC: support for multiple TC reports");
                    else
                        DeCode.Add("TC: reserved");

                    //TS
                    if (bitsOctet[3] == '0')
                        DeCode.Add("TS: no capability to support Target State Reports");
                    else
                        DeCode.Add("TS: capable of supporting target State Reports");

                    //ARV
                    if (bitsOctet[4] == '0')
                        DeCode.Add("ARV: no capability to generate ARV-reports");
                    else
                        DeCode.Add("ARV: capable of generate ARV-reports");

                    //CDTI/A
                    if (bitsOctet[5] == '0')
                        DeCode.Add("CDTI/A: CDTI not operational");
                    else
                        DeCode.Add("CDTI/A: CDTI operational");

                    //not TCAS
                    if (bitsOctet[6] == '0')
                        DeCode.Add("TCAS: TCAS operational");
                    else
                        DeCode.Add("TCAS: TCAS not operational");

                    //SA
                    if (bitsOctet[7] == '0')
                        DeCode.Add("SA: Antenna Diversity");
                    else
                        DeCode.Add("SA: Single Antenna only");



                }
                // 010, Data Source Identification
                else if (Info.DataItemID[1] == "010")
                {
                    //Item 010, Data Source Identifier // He copiado el I010/010, creo que es igual
                    int[] DataOctet = new int[2];
                    DataOctet[0] = Convert.ToInt32(Octets.Dequeue());
                    DataOctet[1] = Convert.ToInt32(Octets.Dequeue());
                    string Lin = "SAC = " + DataOctet[0] + ", SIC = " + DataOctet[1] + "";
                    DeCode.Add(Lin);

                }
                //010, Service Identification
                else if (Info.DataItemID[1] == "015")
                {
                    string DataOctet = Convert.ToString(Octets.Dequeue(), 2).PadLeft(8, '0');
                    DeCode.Add(DataOctet);

                }
                //016, Service Management 
                else if (Info.DataItemID[1] == "016")
                {
                    byte[] RP = new byte[2];
                    RP[0] = Octets.Dequeue();
                    RP[1] = 00000000;
                    int RP_Dec = BitConverter.ToInt16(RP, 0);
                    DeCode.Add("RP=" + Convert.ToString(RP_Dec));
                }
                // 020, Emitter Category
                else if (Info.DataItemID[1] == "020")
                {
                    byte[] ECAT = new byte[2];
                    ECAT[0] = Octets.Dequeue();
                    ECAT[1] = 00000000;
                    int ECAT_Dec = BitConverter.ToInt16(ECAT, 0);
                    if (ECAT_Dec == 0)
                        DeCode.Add("ECAT: No ADS-B Emitter Category Information");
                    else if (ECAT_Dec == 1)
                        DeCode.Add("ECAT: light aircraft <= 15500 lbs");
                    else if (ECAT_Dec == 2)
                        DeCode.Add("ECAT: 15500 lbs < small aircraft <75000 lbs");
                    else if (ECAT_Dec == 3)
                        DeCode.Add("ECAT: 75000 lbs < medium a/c < 300000 lbs");
                    else if (ECAT_Dec == 4)
                        DeCode.Add("ECAT: High Vortex Large");
                    else if (ECAT_Dec == 5)
                        DeCode.Add("ECAT: 300000 lbs <= heavy aircraft");
                    else if (ECAT_Dec == 6)
                        DeCode.Add("ECAT: highly manoeuvrable (5g acceleration capability) and high speed(> 400 knots cruise)");
                    else if (ECAT_Dec <= 9 && ECAT_Dec >= 7)
                        DeCode.Add("ECAT: reserved");
                    else if (ECAT_Dec == 10)
                        DeCode.Add("ECAT: rotocraft");
                    else if (ECAT_Dec == 11)
                        DeCode.Add("ECAT: glider / sailplane");
                    else if (ECAT_Dec == 12)
                        DeCode.Add("ECAT: lighter-than-air");
                    else if (ECAT_Dec == 13)
                        DeCode.Add("ECAT: unmanned aerial vehicle");
                    else if (ECAT_Dec == 14)
                        DeCode.Add("ECAT: space / transatmospheric vehicle");
                    else if (ECAT_Dec == 15)
                        DeCode.Add("ECAT: ultralight / handglider / paraglider");
                    else if (ECAT_Dec == 16)
                        DeCode.Add("ECAT: parachutist / skydiver");
                    else if (ECAT_Dec <= 19 && ECAT_Dec >= 17)
                        DeCode.Add("ECAT: reserved");
                    else if (ECAT_Dec == 20)
                        DeCode.Add("ECAT: surface emergency vehicle");
                    else if (ECAT_Dec == 21)
                        DeCode.Add("ECAT: surface service vehicle");
                    else if (ECAT_Dec == 22)
                        DeCode.Add("ECAT: fixed ground or tethered obstruction");
                    else if (ECAT_Dec == 23)
                        DeCode.Add("ECAT: cluster obstacle");
                    else if (ECAT_Dec == 24)
                        DeCode.Add("ECAT: line obstacle");
                }
                //040, Target Report Descriptor
                else if (Info.DataItemID[1] == "040")
                {


                    string DataOctet = Convert.ToString(Octets.Dequeue(), 2).PadLeft(8, '0');
                    char[] bitsOctet = DataOctet.ToCharArray();

                    string ATP = "" + bitsOctet[0] + "" + bitsOctet[1] + "" + bitsOctet[2] + "";
                    if (ATP == "000")
                        DeCode.Add("ATP: 24-Bit ICAO address");
                    else if (ATP == "001")
                        DeCode.Add("ATP: Duplicate address");
                    else if (ATP == "010")
                        DeCode.Add("ATP: Surface vehicle address");
                    else if (ATP == "011")
                        DeCode.Add("ATP: Anonymous address");
                    else
                        DeCode.Add("ATP: Reserved for future use");

                    string ARC = "" + bitsOctet[3] + "" + bitsOctet[4] + "";
                    if (ARC == "00")
                        DeCode.Add("ARC: 25 ft");
                    if (ARC == "01")
                        DeCode.Add("ARC: 100 ft");
                    if (ARC == "10")
                        DeCode.Add("ARC: Unknown");
                    else
                        DeCode.Add("ARC: Invalid");

                    if (bitsOctet[5] == '0')
                        DeCode.Add("RC: Default");
                    else
                        DeCode.Add("RC: Range Check passed, CPR Validation pending");

                    if (bitsOctet[6] == '0')
                        DeCode.Add("RAB: Report from target transponde");
                    else
                        DeCode.Add("RAB: Report from field monitor (fixed transponder)");

                    if (bitsOctet[7] == '1')
                    {
                        // 040, First Extension
                        DataOctet = Convert.ToString(Octets.Dequeue(), 2).PadLeft(8, '0');
                        bitsOctet = DataOctet.ToCharArray();

                        if (bitsOctet[0] == 0)
                            DeCode.Add("DCR: No differential correction (ADS-B)");
                        else
                            DeCode.Add("DCR: Differential correction (ADS-B)");
                        if (bitsOctet[1] == 0)
                            DeCode.Add("GBS: Ground Bit not set");
                        else
                            DeCode.Add("GBS: Ground Bit set");
                        if (bitsOctet[2] == 0)
                            DeCode.Add("SIM: Actual target report ");
                        else
                            DeCode.Add("SIM: Simulated target report");
                        if (bitsOctet[3] == 0)
                            DeCode.Add("TST: Default ");
                        else
                            DeCode.Add("TST: Test Target ");
                        if (bitsOctet[4] == 0)
                            DeCode.Add("SSA: Equipment capable to provide Selected Altitude");
                        else
                            DeCode.Add("SSA: Equipment not capable to provide Selected Altitude");
                        string CL = "" + bitsOctet[5] + "" + bitsOctet[6] + "";
                        if (CL == "00")
                            DeCode.Add("CL: Report valid");
                        else if (CL == "01")
                            DeCode.Add("CL: Report suspect");
                        else if (CL == "10")
                            DeCode.Add("CL: No information");
                        else
                            DeCode.Add("CL: Reserved for future use");
                        if (bitsOctet[7] == '1')
                        {
                            //040, Second Extension : Error Conditions

                            DataOctet = Convert.ToString(Octets.Dequeue(), 2).PadLeft(8, '0');
                            bitsOctet = DataOctet.ToCharArray();
                            if (bitsOctet[2] == 0)
                                DeCode.Add("IPC: Independent Position Check default(see note)");
                            else
                                DeCode.Add("IPC: Independent Position Check failed");

                            if (bitsOctet[3] == 0)
                                DeCode.Add("NOGO: NOGO-bit not set");
                            else
                                DeCode.Add("NOGO: NOGO-bit set");
                            if (bitsOctet[4] == 0)
                                DeCode.Add("CPR: CPR Validation correct");
                            else
                                DeCode.Add("CPR: CPR Validation failed");
                            if (bitsOctet[5] == 0)
                                DeCode.Add("LDPJ: LDPJ not detected");
                            else
                                DeCode.Add("LDPJ: LDPJ detected");
                            if (bitsOctet[6] == 0)
                                DeCode.Add("RCF: default");
                            else
                                DeCode.Add("RCF: Range Check failed");

                        }
                    }
                }
                // 070, Mode 3/A Code in Octal Representation 
                else if (Info.DataItemID[1] == "070")
                {

                }
                //071, Time of Applicability for Position
                else if (Info.DataItemID[1] == "071")
                {
                    byte[] TAP = new byte[4];
                    TAP[3] = 0;
                    TAP[2] = Octets.Dequeue();
                    TAP[1] = Octets.Dequeue();
                    TAP[0] = Octets.Dequeue();
                    int TAP_Dec = BitConverter.ToInt32(TAP, 0);
                    double TAP_Dou = Convert.ToDouble(TAP_Dec) / 128;
                    DeCode.Add(Convert.ToString(TAP_Dou));
                }
                //072, Time of Applicability for Velocity
                else if (Info.DataItemID[1] == "072")
                {
                    byte[] TAV = new byte[4];
                    TAV[3] = 0;
                    TAV[2] = Octets.Dequeue();
                    TAV[1] = Octets.Dequeue();
                    TAV[0] = Octets.Dequeue();
                    int TAV_Dec = BitConverter.ToInt32(TAV, 0);
                    double TAV_Dou = Convert.ToDouble(TAV_Dec) / 128;
                    DeCode.Add(Convert.ToString(TAV_Dou));
                }
                //073, Time of Message Reception for Positio
                else if (Info.DataItemID[1] == "073")
                {
                    byte[] TMRP = new byte[4];
                    TMRP[3] = 0;
                    TMRP[2] = Octets.Dequeue();
                    TMRP[1] = Octets.Dequeue();
                    TMRP[0] = Octets.Dequeue();
                    int TMRP_Dec = BitConverter.ToInt32(TMRP, 0);
                    double TMRP_Dou = Convert.ToDouble(TMRP_Dec) / 128;
                    DeCode.Add(Convert.ToString(TMRP_Dou));
                }
                //074, Time of Message Reception of Position–High Precision
                //Inacabado, esta mal planteado
                else if (Info.DataItemID[1] == "074")
                {
                    string DataOctet = Convert.ToString(Octets.Dequeue(), 2).PadLeft(8, '0') + "" + Convert.ToString(Octets.Dequeue(), 2).PadLeft(8, '0') + "" + Convert.ToString(Octets.Dequeue(), 2).PadLeft(8, '0') + "" + Convert.ToString(Octets.Dequeue(), 2).PadLeft(8, '0') + "";
                    char[] bitsOctet = DataOctet.ToCharArray();
                    string FSI = "" + bitsOctet[0] + "" + bitsOctet[1] + "";
                    if (FSI == "11")
                        DeCode.Add("FSI: Reserved");
                    if (FSI == "10")
                        DeCode.Add("TOMRp whole seconds = (I021 / 073) Whole seconds – 1");
                    if (FSI == "01")
                        DeCode.Add("TOMRp whole seconds = (I021 / 073) Whole seconds + 1");
                    else
                        DeCode.Add("TOMRp whole seconds = (I021 / 073) Whole seconds");

                    string TMRPh = "";
                    int i = 0;
                    foreach (char element in bitsOctet)
                    {
                        if (i > 1)
                        {
                            TMRPh = TMRPh + "" + element;
                            i++;
                        }
                    }
                    int TMPRh_Dec = BitConverter.ToInt32(TMRPh,);


                }
                //DeCode.Add("");
            }
        }
    }

    //Representació del DataItem
    public class DataItem
    {
        public int FRN;
        public int FRN_B; //index en el byte
        public string[] DataItemID;
        public string Nom;
        public int Len; // 1+ = 0; 1+2n o 1+8n = 102 o 108

        public List<string> units = new List<string>(); //Si expresa algun tipus de valor, les unitats al mateix index que el valor

        public DataItem()
        { }

        public DataItem(string[] Lin)
        {
            FRN = Convert.ToInt32(Lin[0]);
            FRN_B = Convert.ToInt32(Lin[1]);
            DataItemID = Lin[2].Split('/');
            Nom = Lin[3];
            Len = Convert.ToInt32(Lin[4]);
        }
    }

    //Llibreria amb informació de cada DataItem
    public class CatLib
    {
        public int Num;
        public List<DataItem> ItemsCat = new List<DataItem>();

        public CatLib(int A)
        {
            Num = A;
        }
    }

    //A la espera de ser obsolet
    public class Hertz_Hülsmeyer
    {
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

        public static void test()
        {
            //DataBlock Test = new DataBlock(Data[0]);
            //int A = 0;
            //byte[] A = new byte[2];
            //A[1] = Convert.ToByte("00000001");
            //A[0] = Convert.ToByte(0);
            //byte[] A = new byte[2];
            //A[1] = Convert.ToByte("11111000",2);
            //A[0] = Convert.ToByte("00110000",2);
            //int Fin = BitConverter.ToInt16(A, 0);
            //"0101111\0"

            //byte As = new byte();
            //string g = "10110001";
            //As = Convert.ToByte(g);
        }


    }



    
}
