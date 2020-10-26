﻿using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics.PerformanceData;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Net.Http;
using System.Runtime.Remoting.Metadata.W3cXsd2001;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PGTA_P1
{
    //Representació del DataBlock
    public class DataBlock
    {
        public string ID_Intern;
        List<byte> Original = new List<byte>();

        public string Cat;
        byte[] Long = new byte[2];
        string FSPEL = "";
        public List<DataField> DataFields = new List<DataField>();

        CatLib ItemsCatInfo;
        public string From = "No Data"; //ADS-B, SMR, otro
        public string ID = "No Data";
        string Vehicle = "No Data";
        public string TargetID;

        //A part de construir el DataBlock s'encarrega de repartir la info binaria en cada DataField
        public DataBlock(Queue<byte> Bytes, CatLib[] Categories, int id)
        {
            if (Bytes.Count != 0)
            {
                this.ID_Intern = Convert.ToString(id);
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
                                if (Valorant.DataItemID[1] == "295")
                                {
                                    string TOTAL = "";
                                    bool DataFieldB = true;
                                    while (DataFieldB == true)
                                    {
                                        byte Evaluat = Bytes.Dequeue();
                                        New.Octets.Enqueue(Evaluat); //Afegim al nostra DataField

                                        //Mirem si segueix o no, 0 no segueix 1 si.
                                        string EvaString = Convert.ToString(Evaluat, 2).PadLeft(8, '0');
                                        TOTAL = "" + TOTAL + "" + EvaString + "";
                                        char[] EvaChar = EvaString.ToCharArray();
                                        if (EvaChar[7] == '0')
                                            DataFieldB = false;
                                    }
                                    //Ara ja tenim el FSPEL del item 295, mirem quants datafields te.
                                    char[] TotChar = TOTAL.ToCharArray();
                                    int k = 0;
                                    int instant = 1;
                                    while (k < TotChar.Count())
                                    {
                                        if (TotChar[k] == '1')
                                            if(k != (8*instant)-1)
                                                if(Bytes.Count!= 0)
                                                    New.Octets.Enqueue(Bytes.Dequeue());
                                        k++;
                                    }
                                }
                                else
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
                GetIDandV();
                GetTargetID();
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
                        string H = Evaluat.DeCode[0];
                        if (H == "TYP: Mode S multilateration")
                            this.From = "Multi.";
                        else if (H == "TYP: PSR")
                            this.From = "SMR";
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

        //Busca en els dataItems indicats un identificador del datablock (T. Addres o T. Identification), també busca el tipus de vehicle.
        private void GetIDandV()
        {
            //ID
            int c = DataFields.Count();
            int i = 0; bool e = false;
            while ((i < c) && (e == false))
            {
                DataField Evaluat = DataFields[i];
                if (Cat == "10")
                {
                    if (Evaluat.Info.DataItemID[1] == "245")
                    {
                        e = true;
                        this.ID = Evaluat.DeCode[1];
                    }
                }
                else
                {
                    if (Evaluat.Info.DataItemID[1] == "170")
                    {
                        e = true;
                        this.ID = Evaluat.DeCode[0];
                    }
                }
                i++;
            }
            if (e == false) 
            {
                i = 0;
                while ((i < c) && (e == false))
                {
                    DataField Evaluat = DataFields[i];
                    if (Cat == "10")
                    {
                        if (Evaluat.Info.DataItemID[1] == "220")
                        {
                            e = true;
                            this.ID = Evaluat.DeCode[0];
                        }
                    }
                    else
                    {
                        if (Evaluat.Info.DataItemID[1] == "080")
                        {
                            e = true;
                            this.ID = Evaluat.DeCode[0];
                        }
                    }
                    i++;
                }
                if (e == false)
                {
                    i = 0;
                    while ((i < c) && (e == false))
                    {
                        DataField Evaluat = DataFields[i];
                        if (Cat == "10")
                        {
                            if (Evaluat.Info.DataItemID[1] == "161")
                            {
                                e = true;
                                this.ID = Evaluat.DeCode[0];
                            }
                        }
                        i++;
                    }

                    if (e == false) 
                    {
                        i = 0;
                    while ((i < c) && (e == false))
                    {
                        DataField Evaluat = DataFields[i];
                        if (Cat == "10")
                        {
                            if (Evaluat.Info.DataItemID[1] == "000")
                            {
                                e = true;
                                this.TargetID = "Not a target";
                            }
                        }
                        i++;
                    }
                    }
                }
            }

            //Vehicle
            i = 0; e = false;
            while ((i < c) && (e == false))
            {
                DataField Evaluat = DataFields[i];
                if (Cat == "10")
                {
                    if (Evaluat.Info.DataItemID[1] == "300")
                    {
                        e = true;
                        this.Vehicle = Evaluat.DeCode[0];
                    }
                }
                else
                {
                    if (Evaluat.Info.DataItemID[1] == "020")
                    {
                        e = true;
                        this.Vehicle = Evaluat.DeCode[0];
                    }
                }
                i++;
            }

        }

        private void GetTargetID()
        {
            int c = DataFields.Count();
            int i = 0; bool e = false;
            while ((i < c) && (e == false))
            {
                DataField Evaluat = DataFields[i];
                if (Cat == "10")
                {
                    if (Evaluat.Info.DataItemID[1] == "220")
                    {
                        e = true;
                        this.TargetID = Evaluat.DeCode[0];
                    }
                }
                else
                {
                    if (Evaluat.Info.DataItemID[1] == "080")
                    {
                        e = true;
                        this.TargetID = Evaluat.DeCode[0];
                    }
                }
                i++;
            }
        }

        //Vector per moestrar en DatBlocks (DGW)
        public string[] StringLin()
        {
            string[] Ret = new string[5];
            Ret[0] = Cat;
            Ret[1] = From;
            Ret[2] = ID;
            Ret[3] = Vehicle;
            Ret[4] = ID_Intern;

            return Ret;
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
                    string Lin = "SAC: " + DataOctet[0] + ", SIC: " + DataOctet[1] + "";
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
                        DeCode.Add("ADS-B");
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
                        if (TOT == "00")
                            DeCode.Add("TOT: Undetermined");
                        else if (TOT == "01")
                            DeCode.Add("TOT: Aircraft");
                        else if (TOT == "10")
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
                    DeCode.Add("FATAL ERROR");
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
                    string DataOctet = Convert.ToString(Octets.Dequeue(), 2).PadLeft(8, '0');
                    char[] bitsOctet = DataOctet.ToCharArray();

                    if (bitsOctet[0] == '0')
                        DeCode.Add("V: Code validated");
                    else
                    {
                        DeCode.Add("V: Code not validated");
                        bitsOctet[0] = '0';
                    }
                        

                    if (bitsOctet[1] == '0')
                        DeCode.Add("G: Default");
                    else
                    {
                        DeCode.Add("G: Garbled code");
                        bitsOctet[1] = '0';
                    }
                        

                    if (bitsOctet[2] == '0')
                        DeCode.Add("L: Mode-3/A code derived from the reply of the transponder");
                    else
                    {
                        DeCode.Add("L: Mode-3/A code not extracted during the last scan");
                        bitsOctet[2] = '0';
                    }

                    byte[] New = new byte[2];
                    New[1] = Convert.ToByte(new string(bitsOctet), 2);
                    New[0] = Octets.Dequeue();
                    string OO = Convert.ToString(BitConverter.ToInt16(New, 0),8);
                    DeCode.Add(OO);
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

                    TimeSpan t = TimeSpan.FromSeconds(Time_S);
                    string answer = string.Format("{0:D2}h:{1:D2}m:{2:D2}s:{3:D3}ms",
                                    t.Hours,
                                    t.Minutes,
                                    t.Seconds,
                                    t.Milliseconds);
                    DeCode.Add(answer);

                    this.Info.units.Add("UTC, form midnight");
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
                            if (bitsOctet[0] == 0)
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
                    DeCode.Add(Convert.ToString(Gs_Kt));

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
                    DeCode.Add(Convert.ToString(ByteInt * 0.25));
                    ByteInt = BitConverter.ToInt16(Yc, 0);
                    DeCode.Add(Convert.ToString(ByteInt * 0.25));

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

                    DeCode.Add(Conversion.Hex(BitConverter.ToInt32(TA, 0)));
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

                    int i = 0; int h = 0; int k = 1;
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
                            TarID = new Char[8 - k];
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
                    while (i < Rep - 1)
                    {
                        DataOctet = "" + DataOctet + "" + Convert.ToString(Octets.Dequeue(), 2).PadLeft(8, '0') + "";
                        i++;
                    }

                    char[] bitsOctet = DataOctet.ToCharArray();
                    char[] BDS1 = new char[4]; char[] BDS2 = new char[4];
                    i = 0;
                    while (i < 4)
                    {
                        BDS1[i] = bitsOctet[i];
                        BDS2[i] = bitsOctet[i + 4];
                        i++;
                    }
                    DeCode.Add(DataOctet);
                    DeCode.Add(new string(BDS1));
                    DeCode.Add(new string(BDS2));
                } //NO TEST
                else if (Info.DataItemID[1] == "270")
                {
                    //Item 270, Target Size & Orientation
                    string DataOctet = Convert.ToString(Octets.Dequeue(), 2).PadLeft(8, '0');
                    char[] bitsOctet = DataOctet.ToCharArray();

                    //Lenght
                    int i = 1; char[] NewB = new char[8]; NewB[0] = '0';
                    while (i < 8)
                    {
                        NewB[i] = bitsOctet[i - 1];
                        i++;
                    }
                    int Dat = Convert.ToByte(new string(NewB), 2);
                    DeCode.Add(Convert.ToString(Dat));
                    this.Info.units.Add("m");

                    //First FX
                    if (bitsOctet[7] == '1')
                    {
                        DataOctet = Convert.ToString(Octets.Dequeue(), 2).PadLeft(8, '0');
                        bitsOctet = DataOctet.ToCharArray();

                        //Orientacio
                        i = 1; NewB = new char[8]; NewB[0] = '0';
                        while (i < 8)
                        {
                            NewB[i] = bitsOctet[i - 1];
                            i++;
                        }
                        Dat = Convert.ToByte(new string(NewB), 2);
                        double Or = Dat * 360 / 128;
                        DeCode.Add(Convert.ToString(Or));
                        this.Info.units.Add("º");

                        //Second FX
                        if (bitsOctet[7] == '1')
                        {
                            DataOctet = Convert.ToString(Octets.Dequeue(), 2).PadLeft(8, '0');
                            bitsOctet = DataOctet.ToCharArray();

                            //Width
                            i = 1; NewB = new char[8]; NewB[0] = '0';
                            while (i < 8)
                            {
                                NewB[i] = bitsOctet[i - 1];
                                i++;
                            }
                            Dat = Convert.ToByte(new string(NewB), 2);
                            DeCode.Add(Convert.ToString(Dat));
                            this.Info.units.Add("m");
                        }
                    }
                }
                else if (Info.DataItemID[1] == "280")
                {
                    //Item 280, Presence
                    int Rep = Octets.Dequeue();
                    int i = 0;
                    while (i < Rep)
                    {
                        //DRHO
                        string DRHO = Convert.ToString(Octets.Dequeue(), 2).PadLeft(8, '0');
                        char[] bitsOctet = DRHO.ToCharArray(); int DRHO_Dec = 0;
                        if (bitsOctet[0] == '1')
                        {
                            int z = 0;
                            while (z < bitsOctet.Length)
                            {
                                if (bitsOctet[z] == '1')
                                    bitsOctet[z] = '0';
                                else
                                    bitsOctet[z] = '1';
                                z++;
                            }
                            DRHO_Dec = Convert.ToInt32(new string(bitsOctet), 2) * (-1);
                        }
                        else
                        {
                            DRHO_Dec = Convert.ToInt32(new string(bitsOctet), 2);
                        }
                        DeCode.Add(Convert.ToString(DRHO_Dec));
                        this.Info.units.Add("m");

                        //DTHETA
                        string DTHETA = Convert.ToString(Octets.Dequeue(), 2).PadLeft(8, '0');
                        bitsOctet = DTHETA.ToCharArray(); double DTHETA_Dec = 0;
                        if (bitsOctet[0] == '1')
                        {
                            int z = 0;
                            while (z < bitsOctet.Length)
                            {
                                if (bitsOctet[z] == '1')
                                    bitsOctet[z] = '0';
                                else
                                    bitsOctet[z] = '1';
                                z++;
                            }
                            DTHETA_Dec = Convert.ToInt32(new string(bitsOctet), 2) * (-1) * 0.15;
                        }
                        else
                        {
                            DTHETA_Dec = Convert.ToInt32(new string(bitsOctet), 2) * 0.15;
                        }
                        DeCode.Add(Convert.ToString(DTHETA_Dec));
                        this.Info.units.Add("º");

                        i++;
                    }
                } //NO TEST
                else if (Info.DataItemID[1] == "300")
                {
                    //Item 300, Vehicle Fleet Identification 
                    int VFI = Octets.Dequeue();
                    if (VFI == 0)
                        DeCode.Add("Unknown");
                    else if (VFI == 1)
                        DeCode.Add("ATC equipment maintenance");
                    else if (VFI == 2)
                        DeCode.Add("Airport maintenance");
                    else if (VFI == 3)
                        DeCode.Add("Fire");
                    else if (VFI == 4)
                        DeCode.Add("Bird scarer");
                    else if (VFI == 5)
                        DeCode.Add("Snow plough");
                    else if (VFI == 6)
                        DeCode.Add("Runway sweeper");
                    else if (VFI == 7)
                        DeCode.Add("Emergency");
                    else if (VFI == 8)
                        DeCode.Add("Police");
                    else if (VFI == 9)
                        DeCode.Add("Bus");
                    else if (VFI == 10)
                        DeCode.Add("Tug (push/tow)");
                    else if (VFI == 11)
                        DeCode.Add("Grass cutter");
                    else if (VFI == 12)
                        DeCode.Add("Fuel");
                    else if (VFI == 13)
                        DeCode.Add("Baggage");
                    else if (VFI == 14)
                        DeCode.Add("Catering");
                    else if (VFI == 15)
                        DeCode.Add("Aircraft maintenance");
                    else
                        DeCode.Add("Flyco (follow me)");
                } //NO TEST
                else if (Info.DataItemID[1] == "310")
                {
                    //Item 310, Pre-programmed Message 
                    string DataOctet = Convert.ToString(Octets.Dequeue(), 2).PadLeft(8, '0');
                    char[] bitsOctet = DataOctet.ToCharArray();

                    //TRB
                    if (bitsOctet[0] == '0')
                        DeCode.Add("TRB: Default");
                    else
                        DeCode.Add("TRB: In Trouble");

                    //MSG
                    bitsOctet[0] = '0';
                    int MSG = Convert.ToByte(new string(bitsOctet), 2);
                    if (MSG == 1)
                        DeCode.Add("MSG: Towing aircraft");
                    else if (MSG == 2)
                        DeCode.Add("MSG: “Follow me” operation ");
                    else if (MSG == 2)
                        DeCode.Add("MSG: Runway check");
                    else if (MSG == 2)
                        DeCode.Add("MSG: Emergency operation (fire, medical…) ");
                    else
                        DeCode.Add("MSG: Work in progress (maintenance, birds scarer, sweepers…) ");
                } //NO TEST
                else if (Info.DataItemID[1] == "500")
                {
                    //Item 500, Standard Deviation of Position 
                    double X = Octets.Dequeue() * 0.25; DeCode.Add(Convert.ToString(X)); this.Info.units.Add("m (x)");
                    double Y = Octets.Dequeue() * 0.25; DeCode.Add(Convert.ToString(Y)); this.Info.units.Add("m (y)");
                    double XY = Octets.Dequeue() * 0.25; DeCode.Add(Convert.ToString(XY)); this.Info.units.Add("m^2");
                } //NO TEST
                else if (Info.DataItemID[1] == "550")
                {
                    //Item 550, System Status
                    string DataOctet = Convert.ToString(Octets.Dequeue(), 2).PadLeft(8, '0');
                    char[] bitsOctet = DataOctet.ToCharArray();

                    //NOGO
                    string NOGO = "" + bitsOctet[0] + "" + bitsOctet[1] + "";
                    if (NOGO == "00")
                        DeCode.Add("NOGO: Operational");
                    else if (NOGO == "01")
                        DeCode.Add("NOGO: Degraded");
                    else if (NOGO == "10")
                        DeCode.Add("NOGO: NOGO");

                    //OVL
                    if (bitsOctet[2] == '0')
                        DeCode.Add("OVL: No overload");
                    else
                        DeCode.Add("OVL: Overload");

                    //TSV
                    if (bitsOctet[3] == '0')
                        DeCode.Add("TSV: valid");
                    else
                        DeCode.Add("TSV: invalid");

                    //DIV
                    if (bitsOctet[4] == '0')
                        DeCode.Add("DIV: Normal Operation");
                    else
                        DeCode.Add("DIV: Diversity degraded");

                    //TTF
                    if (bitsOctet[5] == '0')
                        DeCode.Add("TTF: Test Target Operative");
                    else
                        DeCode.Add("TTF: Test Target Failure");
                }
                else
                {
                    DeCode.Add("-");
                }
            }
            else
            {
                //Categoria 021
                if (Info.DataItemID[1] == "008")
                {
                    //Item 008, Aircraft Operational Status
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
                else if (Info.DataItemID[1] == "010")
                {
                    // 010, Data Source Identification
                    //Item 010, Data Source Identifier // He copiado el I010/010, creo que es igual
                    int[] DataOctet = new int[2];
                    DataOctet[0] = Convert.ToInt32(Octets.Dequeue());
                    DataOctet[1] = Convert.ToInt32(Octets.Dequeue());
                    string Lin = "SAC: " + DataOctet[0] + ", SIC: " + DataOctet[1] + "";
                    DeCode.Add(Lin);

                }
                else if (Info.DataItemID[1] == "015")
                {
                    //010, Service Identification
                    DeCode.Add(Octets.Dequeue().ToString());
                }
                else if (Info.DataItemID[1] == "016")
                {
                    //016, Service Management 
                    byte[] RP = new byte[2];
                    RP[0] = Octets.Dequeue();

                    RP[1] = 0;
                    double RP_Dec = BitConverter.ToInt16(RP, 0) * 0.5;
                    if (RP_Dec != 0)
                    {
                        DeCode.Add(Convert.ToString(RP_Dec));
                        this.Info.units.Add("s");
                    }
                    else
                        DeCode.Add("RT: Data driven mode Range 0... 127.5 seconds, a value of 127.5 indicates 127.5 seconds or above");
                }
                else if (Info.DataItemID[1] == "020")
                {
                    // 020, Emitter Category
                    byte[] ECAT = new byte[2];
                    ECAT[0] = Octets.Dequeue();
                    ECAT[1] = 00000000;
                    int ECAT_Dec = BitConverter.ToInt16(ECAT, 0);
                    if (ECAT_Dec == 0)
                        DeCode.Add("No ADS-B Emitter Category Information");
                    else if (ECAT_Dec == 1)
                        DeCode.Add("light aircraft");
                    else if (ECAT_Dec == 2)
                        DeCode.Add("small aircraft");
                    else if (ECAT_Dec == 3)
                        DeCode.Add("medium aircraft");
                    else if (ECAT_Dec == 4)
                        DeCode.Add("High Vortex Large");
                    else if (ECAT_Dec == 5)
                        DeCode.Add("heavy aircraft");
                    else if (ECAT_Dec == 6)
                        DeCode.Add("highly manoeuvrable and high speed)");
                    else if (ECAT_Dec <= 9 && ECAT_Dec >= 7)
                        DeCode.Add("reserved");
                    else if (ECAT_Dec == 10)
                        DeCode.Add("rotocraft");
                    else if (ECAT_Dec == 11)
                        DeCode.Add("glider / sailplane");
                    else if (ECAT_Dec == 12)
                        DeCode.Add("lighter-than-air");
                    else if (ECAT_Dec == 13)
                        DeCode.Add("unmanned aerial vehicle");
                    else if (ECAT_Dec == 14)
                        DeCode.Add("space / transatmospheric vehicle");
                    else if (ECAT_Dec == 15)
                        DeCode.Add("ultralight / handglider / paraglider");
                    else if (ECAT_Dec == 16)
                        DeCode.Add("parachutist / skydiver");
                    else if (ECAT_Dec <= 19 && ECAT_Dec >= 17)
                        DeCode.Add("reserved");
                    else if (ECAT_Dec == 20)
                        DeCode.Add("surface emergency vehicle");
                    else if (ECAT_Dec == 21)
                        DeCode.Add("surface service vehicle");
                    else if (ECAT_Dec == 22)
                        DeCode.Add("fixed ground or tethered obstruction");
                    else if (ECAT_Dec == 23)
                        DeCode.Add("cluster obstacle");
                    else if (ECAT_Dec == 24)
                        DeCode.Add("line obstacle");
                }
                else if (Info.DataItemID[1] == "040")
                {
                    //040, Target Report Descriptor
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
                    else if (ARC == "01")
                        DeCode.Add("ARC: 100 ft");
                    else if (ARC == "10")
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

                        if (bitsOctet[0] == '0')
                            DeCode.Add("DCR: No differential correction (ADS-B)");
                        else
                            DeCode.Add("DCR: Differential correction (ADS-B)");
                        if (bitsOctet[1] == '0')
                            DeCode.Add("GBS: Ground Bit not set");
                        else
                            DeCode.Add("GBS: Ground Bit set");
                        if (bitsOctet[2] == '0')
                            DeCode.Add("SIM: Actual target report ");
                        else
                            DeCode.Add("SIM: Simulated target report");
                        if (bitsOctet[3] == '0')
                            DeCode.Add("TST: Default ");
                        else
                            DeCode.Add("TST: Test Target ");
                        if (bitsOctet[4] == '0')
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
                            if (bitsOctet[1] == '0')
                                DeCode.Add("LLC: Default");
                            else
                                DeCode.Add("LLC: List Lookup failed (see note)");
                            if (bitsOctet[2] == '0')
                                DeCode.Add("IPC: Independent Position Check default(see note)");
                            else
                                DeCode.Add("IPC: Independent Position Check failed");
                            if (bitsOctet[3] == '0')
                                DeCode.Add("NOGO: NOGO-bit not set");
                            else
                                DeCode.Add("NOGO: NOGO-bit set");
                            if (bitsOctet[4] == '0')
                                DeCode.Add("CPR: CPR Validation correct");
                            else
                                DeCode.Add("CPR: CPR Validation failed");
                            if (bitsOctet[5] == '0')
                                DeCode.Add("LDPJ: LDPJ not detected");
                            else
                                DeCode.Add("LDPJ: LDPJ detected");
                            if (bitsOctet[6] == '0')
                                DeCode.Add("RCF: default");
                            else
                                DeCode.Add("RCF: Range Check failed");
                        }
                    }
                }
                else if (Info.DataItemID[1] == "070")
                {
                    // 070, Mode 3/A Code in Octal Representation 
                    byte []b = new byte[2];
                    b[1] = Octets.Dequeue();
                    b[0] = Octets.Dequeue();
                    int Oc = BitConverter.ToInt16(b, 0);
                    string OO = Convert.ToString(Oc, 8);
                    DeCode.Add(OO);
                }
                else if (Info.DataItemID[1] == "071")
                {
                    //071, Time of Applicability for Position
                    byte[] TAP = new byte[4];
                    TAP[3] = 0;
                    TAP[2] = Octets.Dequeue();
                    TAP[1] = Octets.Dequeue();
                    TAP[0] = Octets.Dequeue();
                    int TAP_Dec = BitConverter.ToInt32(TAP, 0);
                    double TAP_Dou = Convert.ToDouble(TAP_Dec) / 128;
                    TimeSpan t = TimeSpan.FromSeconds(TAP_Dou);
                    string answer = string.Format("{0:D2}h:{1:D2}m:{2:D2}s:{3:D3}ms",
                                    t.Hours,
                                    t.Minutes,
                                    t.Seconds,
                                    t.Milliseconds);
                    DeCode.Add(answer);

                    this.Info.units.Add("UTC");
                }
                else if (Info.DataItemID[1] == "072")
                {
                    //072, Time of Applicability for Velocity
                    byte[] TAV = new byte[4];
                    TAV[3] = 0;
                    TAV[2] = Octets.Dequeue();
                    TAV[1] = Octets.Dequeue();
                    TAV[0] = Octets.Dequeue();
                    int TAV_Dec = BitConverter.ToInt32(TAV, 0);
                    double TAV_Dou = Convert.ToDouble(TAV_Dec) / 128;
                    DeCode.Add(Convert.ToString(TAV_Dou));
                    this.Info.units.Add("s");
                } //NO TEST
                else if (Info.DataItemID[1] == "073")
                {
                    //073, Time of Message Reception for Positio
                    byte[] TMRP = new byte[4];
                    TMRP[3] = 0;
                    TMRP[2] = Octets.Dequeue();
                    TMRP[1] = Octets.Dequeue();
                    TMRP[0] = Octets.Dequeue();
                    int TMRP_Dec = BitConverter.ToInt32(TMRP, 0);
                    double TMRP_Dou = Convert.ToDouble(TMRP_Dec) / 128;

                    TimeSpan t = TimeSpan.FromSeconds(TMRP_Dou);
                    string answer = string.Format("{0:D2}h:{1:D2}m:{2:D2}s:{3:D3}ms",
                                    t.Hours,
                                    t.Minutes,
                                    t.Seconds,
                                    t.Milliseconds);
                    DeCode.Add(answer);

                    this.Info.units.Add("UTC");
                }
                else if (Info.DataItemID[1] == "074")
                {
                    //074, Time of Message Reception of Position–High Precision
                    byte dequeed = Octets.Dequeue();
                    string FSI_str = Convert.ToString(dequeed);
                    char[] FSI_vec = FSI_str.ToCharArray();
                    string FSI = Convert.ToString("" + FSI_vec[0] + "" + FSI_vec[1] + "");
                    if (FSI == "11")
                        DeCode.Add("FSI: Reserved");
                    if (FSI == "10")
                        DeCode.Add("TOMRp whole seconds = (I021 / 073) Whole seconds – 1");
                    if (FSI == "01")
                        DeCode.Add("TOMRp whole seconds = (I021 / 073) Whole seconds + 1");
                    else
                        DeCode.Add("TOMRp whole seconds = (I021 / 073) Whole seconds");

                    FSI_vec[0] = '0';
                    FSI_vec[1] = '0';

                    byte[] TOMRPh = new byte[4];
                    string s = Convert.ToString(FSI_vec);
                    TOMRPh[3] = Convert.ToByte(s);
                    TOMRPh[2] = Octets.Dequeue();

                    TOMRPh[1] = Octets.Dequeue();
                    TOMRPh[0] = Octets.Dequeue();
                    int TOMRPh_Dec = (BitConverter.ToInt32(TOMRPh, 0)) / (2 ^ 30);

                    TimeSpan t = TimeSpan.FromSeconds(TOMRPh_Dec);
                    string answer = string.Format("{0:D2}h:{1:D2}m:{2:D2}s:{3:D3}ms",
                                    t.Hours,
                                    t.Minutes,
                                    t.Seconds,
                                    t.Milliseconds);
                    DeCode.Add(answer);

                    this.Info.units.Add("UTC");
                }
                else if (Info.DataItemID[1] == "075")
                {
                    //075, Time of Message Reception for Velocity
                    byte[] TMRV = new byte[4];

                    TMRV[3] = 0;
                    TMRV[2] = Octets.Dequeue();
                    TMRV[1] = Octets.Dequeue();
                    TMRV[0] = Octets.Dequeue();

                    double TMRV_Dec = (BitConverter.ToInt32(TMRV, 0)) / 128;
                    TimeSpan t = TimeSpan.FromSeconds(TMRV_Dec);
                    string answer = string.Format("{0:D2}h:{1:D2}m:{2:D2}s:{3:D3}ms",
                                    t.Hours,
                                    t.Minutes,
                                    t.Seconds,
                                    t.Milliseconds);
                    DeCode.Add(answer);

                    this.Info.units.Add("UTC");
                }
                else if (Info.DataItemID[1] == "076")
                {
                    //076, Time of Message Reception of Velocity–High Precision 
                    byte dequeed = Octets.Dequeue();
                    string FSI_str = Convert.ToString(dequeed);
                    char[] FSI_vec = FSI_str.ToCharArray();
                    string FSI = Convert.ToString("" + FSI_vec[0] + "" + FSI_vec[1] + "");
                    if (FSI == "11")
                        DeCode.Add("FSI: Reserved");
                    if (FSI == "10")
                        DeCode.Add("TOMRp whole seconds = (I021 / 075) Whole seconds – 1");
                    if (FSI == "01")
                        DeCode.Add("TOMRp whole seconds = (I021 / 075) Whole seconds + 1");
                    else
                        DeCode.Add("TOMRp whole seconds = (I021 / 075) Whole seconds");

                    FSI_vec[0] = '0';
                    FSI_vec[1] = '0';

                    byte[] TOMRVh = new byte[4];
                    string s = Convert.ToString(FSI_vec);
                    TOMRVh[3] = Convert.ToByte(s);
                    TOMRVh[2] = Octets.Dequeue();

                    TOMRVh[1] = Octets.Dequeue();
                    TOMRVh[0] = Octets.Dequeue();
                    int TOMRVh_Dec = (BitConverter.ToInt32(TOMRVh, 0)) / (2 ^ 30);
                    DeCode.Add(Convert.ToString(TOMRVh_Dec));
                    this.Info.units.Add("s");
                } //NO TEST
                else if (Info.DataItemID[1] == "077")
                {
                    //077, Time of ASTERIX Report Transmission 
                    byte[] TART = new byte[4];

                    TART[3] = 0;
                    TART[2] = Octets.Dequeue();
                    TART[1] = Octets.Dequeue();
                    TART[0] = Octets.Dequeue();

                    double TART_Dec = (BitConverter.ToInt32(TART, 0)) / 128;
                    TimeSpan t = TimeSpan.FromSeconds(TART_Dec);
                    string answer = string.Format("{0:D2}h:{1:D2}m:{2:D2}s:{3:D3}ms",
                                    t.Hours,
                                    t.Minutes,
                                    t.Seconds,
                                    t.Milliseconds);
                    DeCode.Add(answer);

                    this.Info.units.Add("UTC");
                }
                else if (Info.DataItemID[1] == "080")
                {
                    //080, Target Address
                    byte[] TA = new byte[4];
                    TA[3] = 0;
                    TA[2] = Octets.Dequeue();
                    TA[1] = Octets.Dequeue();
                    TA[0] = Octets.Dequeue();

                    DeCode.Add(Conversion.Hex(BitConverter.ToInt32(TA, 0)));
                }
                else if (Info.DataItemID[1] == "090")
                {
                    //090, Quality Indicators
                    string dataOctet = Convert.ToString(Octets.Dequeue(), 2).PadLeft(8, '0');
                    char[] dataVec = dataOctet.ToCharArray();

                    //NUCNAC
                    string NUCNAC = "" + dataVec[0] + "" + dataVec[1] + "" + dataVec[2] + "";
                    int NucNac = Convert.ToByte(NUCNAC, 2);
                    DeCode.Add("NUCr or NACv: " + NucNac + "");

                    //NUCNIC
                    string NUCNIC = "" + dataVec[3] + "" + dataVec[4] + "" + dataVec[5] + "" + dataVec[6] + "";
                    int NucNic = Convert.ToByte(NUCNIC, 2);
                    DeCode.Add("NUCp or NIC: " + NucNic + "");

                    //FX
                    if (dataVec[7] == '1')
                    {
                        dataOctet = Convert.ToString(Octets.Dequeue(), 2).PadLeft(8, '0');
                        dataVec = dataOctet.ToCharArray();

                        //NICbaro
                        DeCode.Add("NICbaro: " + dataVec[0] + "");

                        //SIL
                        string SIL = "" + dataVec[1] + "" + dataVec[2] + "";
                        int Sil = Convert.ToByte(SIL, 2);
                        DeCode.Add("SIL: " + Sil + "");

                        //NACp
                        string NACp = "" + dataVec[3] + "" + dataVec[4] + "" + dataVec[5] + "" + dataVec[6] + "";
                        int Nacp = Convert.ToByte(NACp, 2);
                        DeCode.Add("NACp: " + Nacp + "");

                        //FX
                        if (dataVec[7] == '1')
                        {
                            dataOctet = Convert.ToString(Octets.Dequeue(), 2).PadLeft(8, '0');
                            dataVec = dataOctet.ToCharArray();

                            //SIL2
                            DeCode.Add("Sil sup: " + dataVec[2] + "");

                            //SDA
                            string SDA = "" + dataVec[3] + "" + dataVec[4] + "";
                            int Sda = Convert.ToByte(SDA, 2);
                            DeCode.Add("SDA: " + Sda + "");

                            //GVA
                            string GVA = "" + dataVec[5] + "" + dataVec[6] + "";
                            int Gva = Convert.ToByte(GVA, 2);
                            DeCode.Add("GVA: " + Gva + "");

                            //FX
                            if (dataVec[7] == '1')
                            {
                                dataOctet = Convert.ToString(Octets.Dequeue(), 2).PadLeft(8, '0');
                                dataVec = dataOctet.ToCharArray();

                                //PIC 
                                string Pic = "" + dataVec[0] + "" + dataVec[1] + "" + dataVec[2] + "" + dataVec[3] + "";
                                int PIC = Convert.ToByte(Pic, 2);
                                if (PIC == 14)
                                    DeCode.Add("PIC: < 0.004 NM");
                                else if (PIC == 13)
                                    DeCode.Add("PIC: < 0.0013 NM");
                                else if (PIC == 12)
                                    DeCode.Add("PIC: < 0.04 NM");
                                else if (PIC == 11)
                                    DeCode.Add("PIC: < 0.1 NM");
                                else if (PIC == 10)
                                    DeCode.Add("PIC: < 0.2 NM");
                                else if (PIC == 9)
                                    DeCode.Add("PIC: < 0.3 NM");
                                else if (PIC == 8)
                                    DeCode.Add("PIC: < 0.5 NM");
                                else if (PIC == 7)
                                    DeCode.Add("PIC: < 0.6 NM");
                                else if (PIC == 6)
                                    DeCode.Add("PIC: < 1.0 NM");
                                else if (PIC == 5)
                                    DeCode.Add("PIC: < 2.0 NM");
                                else if (PIC == 4)
                                    DeCode.Add("PIC: < 4.0 NM");
                                else if (PIC == 3)
                                    DeCode.Add("PIC: < 8.0 NM");
                                else if (PIC == 2)
                                    DeCode.Add("PIC: < 10.0 NM");
                                else if (PIC == 1)
                                    DeCode.Add("PIC: < 20.0 NM");
                                else if (PIC == 0)
                                    DeCode.Add("PIC: No integrity (or > 20.0 NM)");
                                else
                                    DeCode.Add("PIC: Not defined");
                            }
                        }
                    }
                }
                else if (Info.DataItemID[1] == "110")
                {
                    //110, Trajectory Intent
                    string DataOctet = Convert.ToString(Octets.Dequeue(), 2).PadLeft(8, '0');
                    char[] bitsOctet = DataOctet.ToCharArray();
                    if (bitsOctet[0] == '0')
                        DeCode.Add("TIS: Absence of Subfield #1");
                    else
                    {
                        DeCode.Add("TIS: Presence of Subfield #1");
                    }
                    if (bitsOctet[1] == '0')
                        DeCode.Add("TID: Absence of Subfield #2");
                    else
                    {
                        DeCode.Add("TID: Presence of Subfield #2");
                    }
                    if (bitsOctet[7] == '0')
                    { }
                    else
                    {
                        DataOctet = Convert.ToString(Octets.Dequeue(), 2).PadLeft(8, '0');
                        bitsOctet = DataOctet.ToCharArray();
                        if (bitsOctet[0] == '0')
                            DeCode.Add("NAV: Trajectory Intent Data is available for this aircraf");
                        else
                            DeCode.Add("NAV: Trajectory Intent Data is not available for this aircraft");

                        if (bitsOctet[0] == '0')
                            DeCode.Add("NVB: Trajectory Intent Data is valid");
                        else
                            DeCode.Add("NVB: Trajectory Intent Data is not valid");
                        if (bitsOctet[7] == '0')
                        { }
                        else
                        {
                            DataOctet = Convert.ToString(Octets.Dequeue(), 2).PadLeft(8, '0');
                            DeCode.Add("REP: " + (Convert.ToInt32(DataOctet), 2).ToString());

                            DataOctet = Convert.ToString(Octets.Dequeue(), 2).PadLeft(8, '0');
                            bitsOctet = DataOctet.ToCharArray();
                            if (bitsOctet[0] == '0')
                                DeCode.Add("TCA: TCP number available");
                            else
                                DeCode.Add("TCA: TCP number not available");

                            if (bitsOctet[0] == '1')
                                DeCode.Add("NC: TCP compliance");
                            else
                                DeCode.Add("NC: TCP non-compliance ");
                            DeCode.Add((Convert.ToInt32("" + bitsOctet[2] + "" + bitsOctet[3] + "" + bitsOctet[4] + "" + bitsOctet[5] + "" + bitsOctet[6] + "" + bitsOctet[7] + ""), 2).ToString());

                            byte[] Alt = new byte[2];
                            Alt[1] = Octets.Dequeue();
                            Alt[0] = Octets.Dequeue();
                            int Alt_Dec = BitConverter.ToInt16(Alt, 0) * 10;
                            DeCode.Add(Alt_Dec.ToString());
                            this.Info.units.Add("ft");

                            byte[] Lat = new byte[4];
                            Lat[3] = 0;
                            Lat[2] = Octets.Dequeue();
                            Lat[1] = Octets.Dequeue();
                            Lat[0] = Octets.Dequeue();
                            double Lat_deg = BitConverter.ToInt32(Lat, 0) * 180 / (2 ^ 23);
                            DeCode.Add(Lat_deg.ToString());
                            this.Info.units.Add("deg");

                            byte[] Long = new byte[4];
                            Long[3] = 0;
                            Long[2] = Octets.Dequeue();
                            Long[1] = Octets.Dequeue();
                            Long[0] = Octets.Dequeue();
                            double Long_deg = BitConverter.ToInt32(Long, 0) * 180 / (2 ^ 23);
                            DeCode.Add(Long_deg.ToString());
                            this.Info.units.Add("deg");

                            DataOctet = Convert.ToString(Octets.Dequeue(), 2).PadLeft(8, '0');
                            bitsOctet = DataOctet.ToCharArray();
                            string PT = Convert.ToString("" + bitsOctet[0] + "" + bitsOctet[1] + "" + bitsOctet[2] + "" + bitsOctet[3] + "");
                            int PT_d = Convert.ToInt16(PT, 2);
                            if (PT_d == 0)
                                DeCode.Add("Point Type: Unknown");
                            if (PT_d == 1)
                                DeCode.Add("Point Type: Fly by waypoint (LT)");
                            if (PT_d == 2)
                                DeCode.Add("Point Type: Fly over waypoint (LT)");
                            if (PT_d == 3)
                                DeCode.Add("Point Type: Hold pattern (LT)");
                            if (PT_d == 4)
                                DeCode.Add("Point Type: Procedure hold (LT)");
                            if (PT_d == 5)
                                DeCode.Add("Point Type: Procedure turn (LT)");
                            if (PT_d == 6)
                                DeCode.Add("Point Type: RF leg (LT)");
                            if (PT_d == 7)
                                DeCode.Add("Point Type: Top of climb (VT)");
                            if (PT_d == 8)
                                DeCode.Add("Point Type: Top of descent (VT)");
                            if (PT_d == 9)
                                DeCode.Add("Point Type: Start of level (VT)");
                            if (PT_d == 10)
                                DeCode.Add("Point Type: Cross-over altitude (VT)");
                            if (PT_d == 11)
                                DeCode.Add("Point Type: Transition altitude (VT)");
                            string TD = Convert.ToString("" + bitsOctet[4] + "" + bitsOctet[5] + "");
                            if (TD == "00")
                                DeCode.Add("TD: N/A");
                            if (TD == "01")
                                DeCode.Add("TD: Turn right ");
                            if (TD == "10")
                                DeCode.Add("TD: Turn left");
                            else
                                DeCode.Add("TD: No turn");
                            if (bitsOctet[6] == '0')
                                DeCode.Add("TRA: TTR not available");
                            else
                                DeCode.Add("TRA: TTR available");
                            if (bitsOctet[6] == '0')
                                DeCode.Add("TOA: TOV available");
                            else
                                DeCode.Add("TOA: TOV not available");
                            byte[] TOV = new byte[4];
                            TOV[3] = 0;
                            TOV[2] = Octets.Dequeue();
                            TOV[1] = Octets.Dequeue();
                            TOV[0] = Octets.Dequeue();
                            int TOV_Dec = BitConverter.ToInt32(TOV, 0);
                            DeCode.Add(TOV_Dec.ToString());
                            this.Info.units.Add("seconds");

                            byte[] TTR = new byte[2];
                            TTR[1] = Octets.Dequeue();
                            TTR[0] = Octets.Dequeue();
                            double TTR_Dec = BitConverter.ToInt16(TTR, 0) / 100;
                            DeCode.Add(TTR_Dec.ToString());
                            this.Info.units.Add("Nm");
                        }
                    }
                }
                else if (Info.DataItemID[1] == "130")
                {
                    //130, Position in WGS - 84 Co - ordinates
                    byte[] Lat = new byte[4];
                    Lat[3] = 0;
                    Lat[2] = Octets.Dequeue();
                    Lat[1] = Octets.Dequeue();
                    Lat[0] = Octets.Dequeue();
                    int A = BitConverter.ToInt32(Lat, 0); double P = 180.0 / 8388608;
                    double Lat_Dec = Convert.ToDouble(A) * P;
                    DeCode.Add(Lat_Dec.ToString());
                    this.Info.units.Add("Lat º");
                    DeCode.Add(GeoAngle.FromDouble(Lat_Dec).ToString());
                    this.Info.units.Add("Lat");

                    byte[] Lon = new byte[4];
                    Lon[3] = 0;
                    Lon[2] = Octets.Dequeue();
                    Lon[1] = Octets.Dequeue();
                    Lon[0] = Octets.Dequeue();
                    double Lon_Dec = Convert.ToDouble(BitConverter.ToInt32(Lon, 0)) * 180 / 8388608;
                    DeCode.Add(Lon_Dec.ToString());
                    this.Info.units.Add("Lon º");
                    DeCode.Add(GeoAngle.FromDouble(Lon_Dec).ToString());
                    this.Info.units.Add("Lon");
                }
                else if (Info.DataItemID[1] == "131")
                {
                    //131, High - Resolution Position in WGS - 84 Co - ordinates
                    byte[] Lat = new byte[4];
                    Lat[3] = Octets.Dequeue();
                    Lat[2] = Octets.Dequeue();
                    Lat[1] = Octets.Dequeue();
                    Lat[0] = Octets.Dequeue();
                    double Lat_Dec = Convert.ToDouble(BitConverter.ToInt32(Lat, 0)) * 180 / (1073741824);
                    DeCode.Add(Lat_Dec.ToString());
                    this.Info.units.Add("Lat º");
                    DeCode.Add(GeoAngle.FromDouble(Lat_Dec).ToString());
                    this.Info.units.Add("Lat");

                    byte[] Lon = new byte[4];
                    Lon[3] = Octets.Dequeue();
                    Lon[2] = Octets.Dequeue();
                    Lon[1] = Octets.Dequeue();
                    Lon[0] = Octets.Dequeue();
                    double Lon_Dec = Convert.ToDouble(BitConverter.ToInt32(Lon, 0)) * 180 / (1073741824);
                    DeCode.Add(Lon_Dec.ToString());
                    this.Info.units.Add("Lon º");
                    DeCode.Add(GeoAngle.FromDouble(Lon_Dec).ToString());
                    this.Info.units.Add("Lon");
                }
                else if (Info.DataItemID[1] == "132")
                {
                    //132, Message Amplitude
                    byte Ax = Octets.Dequeue();

                    int Ax_Dec;
                    if (Ax > 255 / 2)
                    {
                        Ax_Dec = -1 * (255 + 1) + Ax;
                    }
                    else
                        Ax_Dec = Ax;
                    DeCode.Add(Ax_Dec.ToString());
                    this.Info.units.Add("dBm");
                }
                else if (Info.DataItemID[1] == "140")
                {
                    //140, Geometric Height
                    byte[] GH = new byte[2];
                    GH[1] = Octets.Dequeue();
                    GH[0] = Octets.Dequeue();
                    double GH_Dec = BitConverter.ToInt16(GH, 0) * 6.25;
                    DeCode.Add(GH_Dec.ToString());
                    this.Info.units.Add("ft");
                }
                else if (Info.DataItemID[1] == "145")
                {
                    //145, Flight Level
                    byte[] FL = new byte[2];
                    FL[1] = Octets.Dequeue();
                    FL[0] = Octets.Dequeue();
                    double FL_Dec = BitConverter.ToInt16(FL, 0) * 0.25;
                    DeCode.Add(FL_Dec.ToString());
                    this.Info.units.Add("FL");
                }
                else if (Info.DataItemID[1] == "146")
                {
                    //146, Selected Altitude
                    string DataOctet = Convert.ToString(Octets.Dequeue(), 2).PadLeft(8, '0');
                    char[] bitsOctet = DataOctet.ToCharArray();
                    if (bitsOctet[0] == '0')
                        DeCode.Add("SAS: No source information provided");
                    else
                        DeCode.Add("SAS: Source information provided");
                    string Sour = Convert.ToString("" + bitsOctet[1] + "" + bitsOctet[2] + "");
                    if (Sour == "00")
                        DeCode.Add("Source: Unknown");
                    else if (Sour == "01")
                        DeCode.Add("Source: Aircraft Altitude (Holding Altitude)");
                    else if (Sour == "10")
                        DeCode.Add("Source: MCP/FCU Selected Altitude");
                    else
                        DeCode.Add("Source: FMS Selected Altitude");
                    if (bitsOctet[3] == '1')
                    {
                        bitsOctet[0] = '1';
                        bitsOctet[1] = '1';
                        bitsOctet[2] = '1';
                    }
                    else
                    {
                        bitsOctet[0] = '0';
                        bitsOctet[1] = '0';
                        bitsOctet[2] = '0';
                    }
                    byte[] Altitude = new byte[2];
                    Altitude[1] = Convert.ToByte(new string(bitsOctet), 2);
                    Altitude[0] = Octets.Dequeue();
                    int Altitude_Dec = BitConverter.ToInt16(Altitude, 0) * 25;
                    this.Info.units.Add("-");
                    this.Info.units.Add("-");
                    this.Info.units.Add("ft");
                }
                else if (Info.DataItemID[1] == "148")
                {
                    //148, Final State Selected Altitude
                    string DataOctet = Convert.ToString(Octets.Dequeue(), 2).PadLeft(8, '0');
                    char[] bitsOctet = DataOctet.ToCharArray();
                    if (bitsOctet[0] == '0')
                        DeCode.Add("MV: Not active or unknown");
                    else
                        DeCode.Add("MV: Active");
                    if (bitsOctet[1] == '0')
                        DeCode.Add("AH: Not active or unknown");
                    else
                        DeCode.Add("AH: Active");
                    if (bitsOctet[2] == '0')
                        DeCode.Add("AM: Not active or unknown");
                    else
                        DeCode.Add("AH: Active");
                    if (bitsOctet[3] == '1')
                    {
                        bitsOctet[0] = '1';
                        bitsOctet[1] = '1';
                        bitsOctet[2] = '1';
                    }
                    else
                    {
                        bitsOctet[0] = '0';
                        bitsOctet[1] = '0';
                        bitsOctet[2] = '0';
                    }
                    byte[] Altitude = new byte[2];
                    string s = bitsOctet.ToString();
                    Altitude[1] = Convert.ToByte(s);
                    Altitude[0] = Octets.Dequeue();
                    int Altitude_Dec = BitConverter.ToInt16(Altitude, 0) * 25;
                    DeCode.Add(Convert.ToString(Altitude_Dec));
                    this.Info.units.Add("ft");
                } //NO TEST
                else if (Info.DataItemID[1] == "150")
                {
                    //151 True Airspeed
                    string DataOctet = Convert.ToString(Octets.Dequeue(), 2).PadLeft(8, '0');
                    char[] bitsOctet = DataOctet.ToCharArray();
                    char IM = bitsOctet[0];
                    if (bitsOctet[0] == '0')
                        DeCode.Add("IM: Air Speed = IAS");
                    else
                        DeCode.Add("IM: Air Speed = Mach");
                    bitsOctet[0] = '0';
                    byte[] IAS = new byte[2];
                    string s = bitsOctet.ToString();
                    IAS[1] = Convert.ToByte(s);
                    IAS[0] = Octets.Dequeue();
                    double IAS_Dec = BitConverter.ToInt16(IAS, 0);
                    if (IM == '0')
                    {
                        IAS_Dec = IAS_Dec * (2 ^ (-14));
                        DeCode.Add(IAS_Dec.ToString());
                        Info.units.Add("NM/s");
                    }
                    else
                    {
                        IAS_Dec = IAS_Dec * (0.001);
                        DeCode.Add(IAS_Dec.ToString());
                    }

                }
                else if (Info.DataItemID[1] == "151")
                {
                    //151 True Airspeed
                    string DataOctet = Convert.ToString(Octets.Dequeue(), 2).PadLeft(8, '0');
                    char[] bitsOctet = DataOctet.ToCharArray();
                    char IM = bitsOctet[0];
                    if (bitsOctet[0] == '0')
                        DeCode.Add("RE: Value in defined range");
                    else
                        DeCode.Add("RE: Value exceeds defined rang");
                    bitsOctet[0] = '0';
                    byte[] TAS = new byte[2];
                    string s = bitsOctet.ToString();
                    TAS[1] = Convert.ToByte(s);
                    TAS[0] = Octets.Dequeue();
                    int TAS_Dec = BitConverter.ToInt16(TAS, 0);
                    DeCode.Add(TAS_Dec.ToString());
                    Info.units.Add("knot");
                }
                else if (Info.DataItemID[1] == "152")
                {
                    //152, Magnetic Heading
                    byte[] MG = new byte[2];
                    MG[1] = Octets.Dequeue();
                    MG[0] = Octets.Dequeue();
                    int MG_Dec = BitConverter.ToInt16(MG, 0) * 360 / (2 ^ 16);
                    DeCode.Add(MG_Dec.ToString());
                    Info.units.Add("º");
                }
                else if (Info.DataItemID[1] == "155")
                {
                    //155, Barometric Vertical Rate
                    string DataOctet = Convert.ToString(Octets.Dequeue(), 2).PadLeft(8, '0');
                    char[] bitsOctet = DataOctet.ToCharArray();
                    char RE = bitsOctet[0];
                    if (bitsOctet[0] == '0')
                    {
                        DeCode.Add("RE: Value in defined range");
                        byte[] BVR = new byte[2];
                        if (bitsOctet[1] == '1')
                            bitsOctet[0] = '1';
                        BVR[1] = Convert.ToByte(new string(bitsOctet), 2);
                        BVR[0] = Octets.Dequeue();
                        double BVR_Dec = BitConverter.ToInt16(BVR, 0) * 6.25;
                        DeCode.Add(BVR_Dec.ToString());
                        Info.units.Add("-");
                        Info.units.Add("feet/minute");
                    }
                    else
                        DeCode.Add("RE: Value exceeds defined rang");
                }
                else if (Info.DataItemID[1] == "157")
                {
                    //160, Airborne Ground Vector
                    string DataOctet = Convert.ToString(Octets.Dequeue(), 2).PadLeft(8, '0');
                    char[] bitsOctet = DataOctet.ToCharArray();
                    char RE = bitsOctet[0];
                    if (bitsOctet[0] == '0')
                        DeCode.Add("RE: Value in defined range");
                    else
                        DeCode.Add("RE: Value exceeds defined rang");
                    bitsOctet[0] = '0';
                    byte[] GVR = new byte[2];
                    string s = bitsOctet.ToString();
                    GVR[1] = Convert.ToByte(new string(bitsOctet), 2);
                    GVR[0] = Octets.Dequeue();
                    double GVR_Dec = BitConverter.ToInt16(GVR, 0) * 6.25;
                    DeCode.Add(GVR_Dec.ToString());
                    Info.units.Add("feet/minute");
                }
                else if (Info.DataItemID[1] == "160")
                {
                    //160, Airborne Ground Vector
                    string DataOctet = Convert.ToString(Octets.Dequeue(), 2).PadLeft(8, '0');
                    char[] bitsOctet = DataOctet.ToCharArray();
                    char RE = bitsOctet[0];
                    if (bitsOctet[0] == '0')
                    {
                        DeCode.Add("RE: Value in defined range");
                        if (bitsOctet[1] == '1')
                            bitsOctet[0] = '1';
                        byte[] GS = new byte[2];
                        GS[1] = Convert.ToByte(new string(bitsOctet), 2);
                        GS[0] = Octets.Dequeue();
                        double GS_Dec = Convert.ToDouble(BitConverter.ToInt16(GS, 0)) / (16384);
                        DeCode.Add(GS_Dec.ToString());
                        Info.units.Add("NM/s");
                        byte[] TA = new byte[4];
                        TA[3] = 0;
                        TA[2] = 0;
                        TA[1] = Octets.Dequeue();
                        TA[0] = Octets.Dequeue();
                        double TA_Dec = Convert.ToDouble(BitConverter.ToInt32(TA, 0)) * 360 / (65536);
                        DeCode.Add(TA_Dec.ToString());
                        Info.units.Add("-");
                        Info.units.Add("º");
                    }
                    else
                        DeCode.Add("RE: Value exceeds defined rang");

                }
                else if (Info.DataItemID[1] == "161")
                {
                    //161, Track Number
                    byte[] TN = new byte[2];
                    TN[1] = Octets.Dequeue();
                    TN[0] = Octets.Dequeue();
                    double TN_Dec = BitConverter.ToInt16(TN, 0);
                    DeCode.Add(TN_Dec.ToString());
                }
                else if (Info.DataItemID[1] == "165")
                {
                    //I021/165 Track Angle Rate 
                    byte[] TAR = new byte[2];
                    TAR[1] = Octets.Dequeue();
                    TAR[0] = Octets.Dequeue();

                    int ByteInt = BitConverter.ToInt16(TAR, 0);
                    DeCode.Add(Convert.ToString(ByteInt * 1 / 32));

                    this.Info.units.Add("°/s");
                } //NO TEST
                else if (Info.DataItemID[1] == "170")
                {

                    //I021/170 Target Identification 
                    //TarID
                    char[] TarID = new char[8];
                    string DataOctet = "" + Convert.ToString(Octets.Dequeue(), 2).PadLeft(8, '0') + "" + Convert.ToString(Octets.Dequeue(), 2).PadLeft(8, '0') + "" + Convert.ToString(Octets.Dequeue(), 2).PadLeft(8, '0') + "" + Convert.ToString(Octets.Dequeue(), 2).PadLeft(8, '0') + "" + Convert.ToString(Octets.Dequeue(), 2).PadLeft(8, '0') + "" + Convert.ToString(Octets.Dequeue(), 2).PadLeft(8, '0') + "";
                    char[] bitsOctet = DataOctet.ToCharArray();

                    int i = 0; int h = 0; int k = 1;
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
                            TarID = new Char[8 - k];
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
                else if (Info.DataItemID[1] == "200")
                {
                    //I021/200 Target Status 
                    string DataOctet = Convert.ToString(Octets.Dequeue(), 2).PadLeft(8, '0');
                    char[] bitsOctet = DataOctet.ToCharArray();

                    //ICF 
                    if (bitsOctet[0] == '0')
                        DeCode.Add("ICF: No intent change active");
                    else
                        DeCode.Add("ICF: Intent change flag raised");

                    //LNAV 
                    if (bitsOctet[1] == '0')
                        DeCode.Add("LNAV: LNAV Mode engaged");
                    else
                        DeCode.Add("LNAV: LNAV Mode not engaged");

                    //PS 
                    byte PS = Convert.ToByte("" + bitsOctet[3] + "" + bitsOctet[4] + "" + bitsOctet[5] + "", 2);
                    if (PS == 0)
                        DeCode.Add("PS: No emergency / not reported");
                    else if (PS == 1)
                        DeCode.Add("PS: General emergency");
                    else if (PS == 2)
                        DeCode.Add("PS: Lifeguard / medical emergency");
                    else if (PS == 3)
                        DeCode.Add("PS: Minimum fuel");
                    else if (PS == 4)
                        DeCode.Add("PS: No communications");
                    else if (PS == 5)
                        DeCode.Add("PS: Unlawful interference");
                    else
                        DeCode.Add("PS: “Downed” Aircraf");

                    //SS
                    byte SS = Convert.ToByte("" + bitsOctet[6] + "" + bitsOctet[7] + "", 2);
                    if (SS == 0)
                        DeCode.Add("SS: No condition reported");
                    else if (SS == 1)
                        DeCode.Add("SS: Permanent Alert (Emergency condition)");
                    else if (SS == 2)
                        DeCode.Add("SS: Temporary Alert (change in Mode 3/A Code other than emergency)");
                    else
                        DeCode.Add("SS: SPI set");
                }
                else if (Info.DataItemID[1] == "210")
                {
                    //I021/210 MOPS Version
                    string DataOctet = Convert.ToString(Octets.Dequeue(), 2).PadLeft(8, '0');
                    char[] bitsOctet = DataOctet.ToCharArray();

                    //VNS
                    if (bitsOctet[1] == '0')
                        DeCode.Add("VNS: The MOPS Version is supported by the GS");
                    else
                        DeCode.Add("VSN: The MOPS Version is not supported by the GS");

                    //VN
                    byte SS = Convert.ToByte("" + bitsOctet[2] + "" + bitsOctet[3] + "" + bitsOctet[4] + "", 2);
                    if (SS == 0)
                        DeCode.Add("VN: ED102/DO-260 [Ref. 8] (0)");
                    else if (SS == 1)
                        DeCode.Add("VN: DO-260A [Ref. 9] (1)");
                    else
                        DeCode.Add("VN: ED102A/DO-260B [Ref. 11] (2)");

                    //LTT
                    byte PS = Convert.ToByte("" + bitsOctet[3] + "" + bitsOctet[4] + "" + bitsOctet[5] + "", 2);
                    if (PS == 0)
                        DeCode.Add("LTT: Othe");
                    else if (PS == 1)
                        DeCode.Add("LTT: UAT");
                    else if (PS == 2)
                        DeCode.Add("LTT: 1090 ES");
                    else if (PS == 3)
                        DeCode.Add("LTT: VDL 4");
                    else
                        DeCode.Add("LTT: Not assigned");
                }
                else if (Info.DataItemID[1] == "220")
                {
                    //I021/220 Met Information
                    string DataOctet = Convert.ToString(Octets.Dequeue(), 2).PadLeft(8, '0');
                    char[] bitsOctet = DataOctet.ToCharArray();

                    //WS
                    if (bitsOctet[0] == '1')
                    {
                        byte Data = Octets.Dequeue();
                        DeCode.Add("WS: " + Data + "");
                        this.Info.units.Add("knot");
                    }

                    //WD
                    if (bitsOctet[1] == '1')
                    {
                        byte Data = Octets.Dequeue();
                        DeCode.Add("WD: " + Data + "");
                        this.Info.units.Add("º");
                    }

                    //TMP
                    if (bitsOctet[2] == '1')
                    {
                        byte Data = Octets.Dequeue();
                        int Ax_Dec;
                        if (Data > 255 / 2)
                        {
                            Ax_Dec = -1 * (255 + 1) + Data;
                        }
                        else
                            Ax_Dec = Data;
                        DeCode.Add("TMP: " + Ax_Dec + "");
                        this.Info.units.Add("ºC");
                    }

                    //TRB
                    if (bitsOctet[3] == '1')
                    {
                        byte Data = Octets.Dequeue();
                        DeCode.Add("TRB: " + Data + "");
                        this.Info.units.Add("-");
                    }
                }
                else if (Info.DataItemID[1] == "230")
                {
                    //I021/230 Roll Angle
                    byte[] Xc = new byte[2];
                    Xc[1] = Octets.Dequeue();
                    Xc[0] = Octets.Dequeue();

                    int ByteInt = BitConverter.ToInt16(Xc, 0);
                    double X = ByteInt * 0.01;
                    DeCode.Add(Convert.ToString(X));

                    this.Info.units.Add("º");
                }
                else if (Info.DataItemID[1] == "250")
                {
                    //Item 250, Mode S MB Data
                    int Rep = Octets.Dequeue();
                    string DataOctet = "";
                    int i = 0;
                    while (i < Rep - 1)
                    {
                        DataOctet = "" + DataOctet + "" + Convert.ToString(Octets.Dequeue(), 2).PadLeft(8, '0') + "";
                        i++;
                    }

                    char[] bitsOctet = DataOctet.ToCharArray();
                    char[] BDS1 = new char[4]; char[] BDS2 = new char[4];
                    i = 0;
                    while (i < 4)
                    {
                        BDS1[i] = bitsOctet[i];
                        BDS2[i] = bitsOctet[i + 4];
                        i++;
                    }
                    DeCode.Add(DataOctet);
                    DeCode.Add(new string(BDS1));
                    DeCode.Add(new string(BDS2));
                } //NO TEST
                else if (Info.DataItemID[1] == "260")
                {
                    //I021/260 ACAS Resolution Advisory Report
                    int i = 0;
                    string DataOctet = "";
                    while (i < 7)
                    {
                        DataOctet = "" + DataOctet + "" + Convert.ToString(Octets.Dequeue(), 2).PadLeft(8, '0') + "";
                        i++;
                    }
                } //NO TEST
                else if (Info.DataItemID[1] == "271")
                {
                    //I021/271 Surface Capabilities and Characteristics
                    string DataOctet = Convert.ToString(Octets.Dequeue(), 2).PadLeft(8, '0');
                    char[] bitsOctet = DataOctet.ToCharArray();

                    //POA
                    if (bitsOctet[2] == '0')
                        DeCode.Add("POA: Position transmitted is not ADS-B position reference point");
                    else
                        DeCode.Add("POA: Position transmitted is the ADS-B position reference point");

                    //CDTI/S
                    if (bitsOctet[3] == '0')
                        DeCode.Add("CDTI/S: CDTI not operationa");
                    else
                        DeCode.Add("CDTI/S: CDTI operational");

                    //B2 low
                    if (bitsOctet[4] == '0')
                        DeCode.Add("B2 low: ≥ 70 Watts");
                    else
                        DeCode.Add("B2 low: < 70 Watts");

                    //RAS
                    if (bitsOctet[5] == '0')
                        DeCode.Add("RAS: Aircraft not receiving ATC-services");
                    else
                        DeCode.Add("RAS: Aircraft receiving ATC services");

                    //IDENT
                    if (bitsOctet[6] == '0')
                        DeCode.Add("IDENT: IDENT switch not active");
                    else
                        DeCode.Add("IDENT: IDENT switch active");

                    //FX
                    if (bitsOctet[7] == '1')
                    {
                        DataOctet = Convert.ToString(Octets.Dequeue(), 2).PadLeft(8, '0');
                        bitsOctet = DataOctet.ToCharArray();

                        byte LW = Convert.ToByte("" + bitsOctet[4] + "" + bitsOctet[5] + "" + bitsOctet[6] + "" + bitsOctet[6] + "", 2);
                        if (LW == 0)
                        {
                            DeCode.Add("L+W (V1,V2): L < 15 W < 11.5");
                        }
                        else if (LW == 1)
                        {
                            DeCode.Add("L+W (V1,V2): L < 15 W < 23");
                        }
                        else if (LW == 2)
                        {
                            DeCode.Add("L+W (V1,V2): L < 25 W < 28.5");
                        }
                        else if (LW == 3)
                        {
                            DeCode.Add("L+W (V1,V2): L < 25 W < 34");
                        }
                        else if (LW == 4)
                        {
                            DeCode.Add("L+W (V1,V2): L < 35 W < 33");
                        }
                        else if (LW == 5)
                        {
                            DeCode.Add("L+W (V1,V2): L < 35 W < 38");
                        }
                        else if (LW == 6)
                        {
                            DeCode.Add("L+W (V1,V2): L < 45 W < 39.5");
                        }
                        else if (LW == 7)
                        {
                            DeCode.Add("L+W (V1,V2): L < 45 W < 45");
                        }
                        else if (LW == 8)
                        {
                            DeCode.Add("L+W (V1,V2): L < 55 W < 45");
                        }
                        else if (LW == 9)
                        {
                            DeCode.Add("L+W (V1,V2): L < 55 W < 52");
                        }
                        else if (LW == 10)
                        {
                            DeCode.Add("L+W (V1,V2): L < 65 W < 59.5");
                        }
                        else if (LW == 11)
                        {
                            DeCode.Add("L+W (V1,V2): L < 65 W < 67");
                        }
                        else if (LW == 12)
                        {
                            DeCode.Add("L+W (V1,V2): L < 75 W < 72.5");
                        }
                        else if (LW == 13)
                        {
                            DeCode.Add("L+W (V1,V2): L < 75 W < 80");
                        }
                        else if (LW == 14)
                        {
                            DeCode.Add("L+W (V1,V2): L < 85 W < 80");
                        }
                        else
                        {
                            DeCode.Add("L+W (V1): L < 85 W > 80");
                            DeCode.Add("L+W (V2): L>85 or W > 80");
                        }
                    }
                }
                else if (Info.DataItemID[1] == "295")
                {
                    //I021/295 Data Ages 
                    string DataOctet1 = Convert.ToString(Octets.Dequeue(), 2).PadLeft(8, '0');
                    char[] bitsOctet1 = DataOctet1.ToCharArray();
                    string DataOctet2;
                    char[] bitsOctet2 = new char[8];
                    string DataOctet3;
                    char[] bitsOctet3 = new char[8];
                    string DataOctet4;
                    char[] bitsOctet4 = new char[8];
                    if (bitsOctet1[7] == '1')
                    {
                        DataOctet2 = Convert.ToString(Octets.Dequeue(), 2).PadLeft(8, '0');
                        bitsOctet2 = DataOctet2.ToCharArray();
                        if (bitsOctet2[7] == '1')
                        {
                            DataOctet3 = Convert.ToString(Octets.Dequeue(), 2).PadLeft(8, '0');
                            bitsOctet3 = DataOctet3.ToCharArray();
                            if (bitsOctet3[7] == '1')
                            {
                                DataOctet4 = Convert.ToString(Octets.Dequeue(), 2).PadLeft(8, '0');
                                bitsOctet4 = DataOctet4.ToCharArray();
                            }
                        }
                    }
                        
                   // DeCode.Add("-");

                    //AOS
                    if (bitsOctet1[0] == '1')
                    {
                        DeCode.Add("Aircraft Operational Status age (295-1)");
                        this.Info.units.Add("-");
                        double AOS = Octets.Dequeue() * 0.1;
                        DeCode.Add(Convert.ToString(AOS));
                        this.Info.units.Add("s");
                    }

                    //TRD
                    if (bitsOctet1[1] == '1')
                    {
                        DeCode.Add("Target Report Descriptor Age (295-2)");
                        this.Info.units.Add("-");
                        double AOS = Octets.Dequeue() * 0.1;
                        DeCode.Add(Convert.ToString(AOS));
                        this.Info.units.Add("s");
                    }

                    //M3A
                    if (bitsOctet1[2] == '1')
                    {
                        DeCode.Add("Mode 3/A Age Age (295-3)");
                        this.Info.units.Add("-");
                        double AOS = Octets.Dequeue() * 0.1;
                        DeCode.Add(Convert.ToString(AOS));
                        this.Info.units.Add("s");
                    }

                    //QI
                    if (bitsOctet1[3] == '1')
                    {
                        DeCode.Add("Quality Indicators Age (295-4)");
                        this.Info.units.Add("-");
                        double AOS = Octets.Dequeue() * 0.1;
                        DeCode.Add(Convert.ToString(AOS));
                        this.Info.units.Add("s");
                    }

                    //TI
                    if (bitsOctet1[4] == '1')
                    {
                        DeCode.Add("Trajectory Intent Age (295-5)");
                        this.Info.units.Add("-");
                        double AOS = Octets.Dequeue() * 0.1;
                        DeCode.Add(Convert.ToString(AOS));
                        this.Info.units.Add("s");
                    }

                    //MAM
                    if (bitsOctet1[5] == '1')
                    {
                        DeCode.Add("Message Amplitude Age (295-6)");
                        this.Info.units.Add("-");
                        double AOS = Octets.Dequeue() * 0.1;
                        DeCode.Add(Convert.ToString(AOS));
                        this.Info.units.Add("s");
                    }

                    //GH
                    if (bitsOctet1[6] == '1')
                    {
                        DeCode.Add("Geometric Heighte Age (295-7)");
                        this.Info.units.Add("-");
                        double AOS = Octets.Dequeue() * 0.1;
                        DeCode.Add(Convert.ToString(AOS));
                        this.Info.units.Add("s");
                    }

                    if (bitsOctet1[7] == '1')
                    {
                        //FL
                        if (bitsOctet2[0] == '1')
                        {
                            DeCode.Add("Flight Level Age (295-8)");
                            this.Info.units.Add("-");
                            double AOS = Octets.Dequeue() * 0.1;
                            DeCode.Add(Convert.ToString(AOS));
                            this.Info.units.Add("s");
                        }

                        //ISA
                        if (bitsOctet2[1] == '1')
                        {
                            DeCode.Add("Intermediate State Selected Altitude Age (295-9)");
                            this.Info.units.Add("-");
                            double AOS = Octets.Dequeue() * 0.1;
                            DeCode.Add(Convert.ToString(AOS));
                            this.Info.units.Add("s");
                        }

                        //FSA
                        if (bitsOctet2[2] == '1')
                        {
                            DeCode.Add("Final State Selected Altitude Age (295-10)");
                            this.Info.units.Add("-");
                            double AOS = Octets.Dequeue() * 0.1;
                            DeCode.Add(Convert.ToString(AOS));
                            this.Info.units.Add("s");
                        }

                        //AS
                        if (bitsOctet2[3] == '1')
                        {
                            DeCode.Add("Air Speed Age (295-11)");
                            this.Info.units.Add("-");
                            double AOS = Octets.Dequeue() * 0.1;
                            DeCode.Add(Convert.ToString(AOS));
                            this.Info.units.Add("s");
                        }

                        //TAS
                        if (bitsOctet2[4] == '1')
                        {
                            DeCode.Add("True Air Speed Age (295-12)");
                            this.Info.units.Add("-");
                            double AOS = Octets.Dequeue() * 0.1;
                            DeCode.Add(Convert.ToString(AOS));
                            this.Info.units.Add("s");
                        }

                        //MH
                        if (bitsOctet2[5] == '1')
                        {
                            DeCode.Add("Magnetic Heading Age (295-13)");
                            this.Info.units.Add("-");
                            double AOS = Octets.Dequeue() * 0.1;
                            DeCode.Add(Convert.ToString(AOS));
                            this.Info.units.Add("s");
                        }

                        //BVR
                        if (bitsOctet2[6] == '1')
                        {
                            DeCode.Add("Barometric Vertical Rate Age (295-14)");
                            this.Info.units.Add("-");
                            double AOS = Octets.Dequeue() * 0.1;
                            DeCode.Add(Convert.ToString(AOS));
                            this.Info.units.Add("s");
                        }

                        if (bitsOctet2[7] == '1')
                        {

                            //GVR
                            if (bitsOctet3[0] == '1')
                            {
                                DeCode.Add("Geometric Vertical Rate Age (295-15)");
                                this.Info.units.Add("-");
                                double AOS = Octets.Dequeue() * 0.1;
                                DeCode.Add(Convert.ToString(AOS));
                                this.Info.units.Add("s");
                            }

                            //GV
                            if (bitsOctet3[1] == '1')
                            {
                                DeCode.Add("Ground Vector Age (295-16)");
                                this.Info.units.Add("-");
                                double AOS = Octets.Dequeue() * 0.1;
                                DeCode.Add(Convert.ToString(AOS));
                                this.Info.units.Add("s");
                            }

                            //TAR
                            if (bitsOctet3[2] == '1')
                            {
                                DeCode.Add("Track Angle Rate Age (295-17)");
                                this.Info.units.Add("-");
                                double AOS = Octets.Dequeue() * 0.1;
                                DeCode.Add(Convert.ToString(AOS));
                                this.Info.units.Add("s");
                            }

                            //TI
                            if (bitsOctet3[3] == '1')
                            {
                                DeCode.Add("Target Identification Age (295-18)");
                                this.Info.units.Add("-");
                                double AOS = Octets.Dequeue() * 0.1;
                                DeCode.Add(Convert.ToString(AOS));
                                this.Info.units.Add("s");
                            }

                            //TI
                            if (bitsOctet3[4] == '1')
                            {
                                DeCode.Add("Target Status Age (295-19)");
                                this.Info.units.Add("-");
                                double AOS = Octets.Dequeue() * 0.1;
                                DeCode.Add(Convert.ToString(AOS));
                                this.Info.units.Add("s");
                            }

                            //MET
                            if (bitsOctet3[5] == '1')
                            {
                                DeCode.Add("Met Information Age (295-20)");
                                this.Info.units.Add("-");
                                double AOS = Octets.Dequeue() * 0.1;
                                DeCode.Add(Convert.ToString(AOS));
                                this.Info.units.Add("s");
                            }

                            //ROA
                            if (bitsOctet3[6] == '1')
                            {
                                DeCode.Add("Roll Angle Age (295-21)");
                                this.Info.units.Add("-");
                                double AOS = Octets.Dequeue() * 0.1;
                                DeCode.Add(Convert.ToString(AOS));
                                this.Info.units.Add("s");
                            }

                            if (bitsOctet3[7] == '1')
                            {
                                //ARA
                                if (bitsOctet4[0] == '1')
                                {
                                    DeCode.Add("ACAS Resolution Advisory Age (295-22)");
                                    this.Info.units.Add("-");
                                    double AOS = Octets.Dequeue() * 0.1;
                                    DeCode.Add(Convert.ToString(AOS));
                                    this.Info.units.Add("s");
                                }

                                //SCC
                                if (bitsOctet4[1] == '1')
                                {
                                    DeCode.Add("Surface Capabilities and Characteristics Age (295-23)");
                                    this.Info.units.Add("-");
                                    double AOS = Octets.Dequeue() * 0.1;
                                    DeCode.Add(Convert.ToString(AOS));
                                    this.Info.units.Add("s");
                                }
                            }
                        }
                    }
                } 
                else if (Info.DataItemID[1] == "400")
                {
                    //I021/400 Receiver ID 
                    byte[] RID = new byte[2];
                    RID[1] = 0;
                    RID[0] = Octets.Dequeue();
                    int RID_Dec = BitConverter.ToInt16(RID, 0);
                    DeCode.Add(RID_Dec.ToString());
                }
                else
                {
                    DeCode.Add("-");
                }
            }
        }

        //Primera linia d'informació més el nom
        public string[] LinVectNom()
        {
            string[] Ret = new string[3];
            Ret[0] = ""+Info.Nom+" ("+Info.DataItemID[1]+")";
            if (DeCode.Count != 0)
                Ret[1] = DeCode[0];
            else
                Ret[1] = "No data";
            if (Info.units.Count() != 0)
                Ret[2] = Info.units[0];
            else
                Ret[2] = "-";

            return Ret;
        }

        //A partir de la segona linia d'informació (sense el nom)
        public string[] LinVect(int i)
        {
            string[] Ret = new string[3];
            Ret[0] = "";
            Ret[1] = DeCode[i];
            if (Info.units.Count() != 0)
                Ret[2] = Info.units[i];
            else
                Ret[2] = "-";

            return Ret;
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

    public class Target
    {
        public List<DataBlock> DataBlocks = new List<DataBlock>();
        public string ID;
        string TargetID;
        string V;

        public Target(List<DataBlock> DataBlocksList)
        {
            DataBlocks = DataBlocksList;
            string[] Info = DataBlocks[0].StringLin();
            ID = Info[2];
            TargetID = DataBlocks[0].TargetID;
            V = Info[3];
            int i = 1; 
            while((V == "No Data")&&(i<DataBlocks.Count()))
            {
                V = DataBlocks[0].StringLin()[3];
                i++;
            }
            
        }

        public Target() { }

        public string[] StringLin()
        {
            string[] Ret = new string[4];
            Ret[0] = ID;
            Ret[1] = TargetID;
            Ret[2] = V;
            Ret[3] = Convert.ToString(DataBlocks.Count());

            return Ret;
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

    //De graus a Graus/Min/Seg
    public class GeoAngle
    {
        public bool IsNegative { get; set; }
        public int Degrees { get; set; }
        public int Minutes { get; set; }
        public int Seconds { get; set; }
        public int Milliseconds { get; set; }

        public static GeoAngle FromDouble(double angleInDegrees)
        {
            //ensure the value will fall within the primary range [-180.0..+180.0]
            while (angleInDegrees < -180.0)
                angleInDegrees += 360.0;

            while (angleInDegrees > 180.0)
                angleInDegrees -= 360.0;

            var result = new GeoAngle();

            //switch the value to positive
            result.IsNegative = angleInDegrees < 0;
            angleInDegrees = Math.Abs(angleInDegrees);

            //gets the degree
            result.Degrees = (int)Math.Floor(angleInDegrees);
            var delta = angleInDegrees - result.Degrees;

            //gets minutes and seconds
            var seconds = (int)Math.Floor(3600.0 * delta);
            result.Seconds = seconds % 60;
            result.Minutes = (int)Math.Floor(seconds / 60.0);
            delta = delta * 3600.0 - seconds;

            //gets fractions
            result.Milliseconds = (int)(1000.0 * delta);

            return result;
        }



        public override string ToString()
        {
            var degrees = this.IsNegative
                ? -this.Degrees
                : this.Degrees;

            return string.Format(
                "{0}° {1:00}' {2:00}\"",
                degrees,
                this.Minutes,
                this.Seconds);
        }



        public string ToString(string format)
        {
            switch (format)
            {
                case "NS":
                    return string.Format(
                        "{0}° {1:00}' {2:00}\".{3:000} {4}",
                        this.Degrees,
                        this.Minutes,
                        this.Seconds,
                        this.Milliseconds,
                        this.IsNegative ? 'S' : 'N');

                case "WE":
                    return string.Format(
                        "{0}° {1:00}' {2:00}\".{3:000} {4}",
                        this.Degrees,
                        this.Minutes,
                        this.Seconds,
                        this.Milliseconds,
                        this.IsNegative ? 'W' : 'E');

                default:
                    throw new NotImplementedException();
            }
        }
    }
}
