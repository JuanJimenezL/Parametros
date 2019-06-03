using System;
using System.Collections.Generic;
using System.Xml;
using System.IO;
using System.IO.Compression;
using System.Data;
using System.Collections;
using System.Linq;

namespace Herramientas {
    public class Parametros : DictionaryBase {
        static public Parametros Parámetros;

        static public string Parámetro(string key) {
            string resultado = "";
            if (Parámetros != null)
                if (Parámetros.Contains(key))
                    resultado = Parámetros[key].ToString();
            if (resultado.Contains("@[")) {
                int Posicion = 0;
                do {
                    Posicion = resultado.IndexOf("@[", Posicion);
                    if (Posicion >= 0) {
                        int PosicionFin = resultado.IndexOf("]", Posicion);
                        PosicionFin = (PosicionFin >= 0) ? PosicionFin : resultado.Length;
                        string Parametro = resultado.Substring(Posicion + 2, PosicionFin - Posicion - 2);
                        resultado = resultado.Substring(0, Posicion) + Parámetro(Parametro) + resultado.Substring(PosicionFin + 1);
                    }
                } while (Posicion >= 0);
            }
            return resultado;       //(Parámetros != null && Parámetros.Contains(key)) ? Parámetros[key].ToString() : "";
        }

        public Object this[String key] {
            get { return (Dictionary[key]); }
            set { Dictionary[key] = value; }
        }

        public ICollection Keys {
            get { return (Dictionary.Keys); }
        }

        public ICollection Values {
            get { return (Dictionary.Values); }
        }

        public void Add(String key, String value) {
            Dictionary.Add(key, value);
        }

        public bool Contains(String key) {
            return (Dictionary.Contains(key));
        }

        public void Remove(String key) {
            Dictionary.Remove(key);
        }

        private Parametros() {
            this.CargarParametros(@".\");
        }

        public Parametros(string NombreFichero) {
            this.CargarParametros(NombreFichero);
        }

        public enum TipoAlmacen {
            Unknown = 0,
            XML = 1,
            Des = 2,
            ZDes = 3,
            Zip = 4,
        }

        // Crear un nuevo FileSystemWatcher y establecer sus propiedades.
        private FileSystemWatcher watcher; // = new FileSystemWatcher();

        private string vPathAlmacen = "";
        private DateTime FechaModificacion { get; set; }
        public string PathAlmacen {
            get { return vPathAlmacen; }
            set {
                FileInfo FI = new FileInfo(value);
                if (vPathAlmacen != value) {
                    vPathAlmacen = value;

                    string NombreFichero = FI.Name;
                    string UbicacionAbsoluta = FI.DirectoryName + "\\";
                    FechaModificacion = FI.LastWriteTime;
                    if (watcher != null) {
                        watcher.Changed -= new FileSystemEventHandler(watcher_Changed);
                        watcher.Dispose();
                        watcher = null;
                    }

                    watcher = new FileSystemWatcher() {
                        Path = UbicacionAbsoluta,               /* Observador en cambios en Fecha de modificación  */
                        NotifyFilter = NotifyFilters.LastWrite, /* Sólo observar el fichero indicado.*/
                        Filter = NombreFichero,                 /* Iniciar la vigilancia.*/
                        EnableRaisingEvents = true
                    };
                    // Añadir manejadores de evento ólo en el cambio.
                    watcher.Changed += new FileSystemEventHandler(watcher_Changed);
                }
            }
        }


        //private DataSet vDataset = null;
        //public DataSet dataset
        //{
        //    get
        //    {
        //        if (vDataset == null) vDataset = ParametrosADataset();

        //        return vDataset;
        //    }
        //}
        public TipoAlmacen Tipo {
            get {
                TipoAlmacen vTipo = TipoAlmacen.Unknown;
                int ind = PathAlmacen.LastIndexOf(".");
                string ext = (ind >= 0) ? PathAlmacen.ToLower().Substring(ind + 1) : "";
                switch (ext) {
                    case "xml": vTipo = TipoAlmacen.XML; break;
                    case "des": vTipo = TipoAlmacen.Des; break;
                    case "zdes": vTipo = TipoAlmacen.ZDes; break;
                    case "zip": vTipo = TipoAlmacen.Zip; break;
                }
                return vTipo;
            }
            set {
                int ind = PathAlmacen.LastIndexOf(".");
                PathAlmacen = (ind >= 0) ? PathAlmacen.Substring(0, ind) : PathAlmacen;
                switch (value) {
                    case TipoAlmacen.XML: PathAlmacen += ".XML"; break;
                    case TipoAlmacen.Des: PathAlmacen += ".Des"; break;
                    case TipoAlmacen.ZDes: PathAlmacen += ".ZDes"; break;
                    case TipoAlmacen.Zip: PathAlmacen += ".Zip"; break;
                }
            }
        }

        public DataSet ParametrosADataset() {
            DataSet ds = new DataSet();
            DataTable dt = new DataTable();
            DataColumn dc = new DataColumn("Key", Type.GetType("System.String"));
            dt.Columns.Add(dc);
            dc = new DataColumn("Value", Type.GetType("System.String"));
            dt.Columns.Add(dc);
            foreach (string s in this.Keys) {
                DataRow dr = dt.NewRow();
                dr["Key"] = s;
                dr["Value"] = this[s];
                dt.Rows.Add(dr);
            }
            ds.Tables.Add(dt);
            return ds;
        }

        public void DatasetAParametros(DataSet vDataset) {
            this.Clear();   // Limpiar colección de parámetros
            foreach (DataRow dr in vDataset.Tables[0].Rows) this[(string)dr["Key"]] = (string)dr["Value"];
        }

        private void ParametrosDesdeXML(XmlDocument xmlPar) {
            this.Clear();   // Limpiar colección de parámetros

            XmlNode root = xmlPar.SelectSingleNode("Parametros");
            foreach (XmlNode n in root.ChildNodes) this[n.Name] = DesencriptarPassword(n.InnerText);
        }

        private XmlDocument ParametrosHaciaXML() {
            XmlDocument xmlPar = new XmlDocument();
            xmlPar.LoadXml("<?xml version=\"1.0\" encoding=\"utf-8\"?><Parametros></Parametros>");

            XmlNode root = xmlPar.SelectSingleNode("Parametros");

            foreach (string clave in KeysToList(this.Keys)) {
                if (clave.Equals("#comment")) {
                    XmlNode param = xmlPar.CreateNode(XmlNodeType.Comment, clave, null);
                    param.InnerText = EncriptarPassword((string)(this[clave] ?? ""));
                    root.AppendChild(param);
                }
                else {
                    XmlNode param = xmlPar.CreateNode(XmlNodeType.Element, clave, null);
                    param.InnerText = EncriptarPassword((this[clave] == null) ? "" : (string)this[clave]);
                    root.AppendChild(param);
                }
            }
            return xmlPar;
        }

        private string DesencriptarPassword(string parametro) {
            int pos1 = parametro.ToUpper().IndexOf("PASSWORD=[[");
            if (pos1 >= 0) {
                pos1 += 11;
                int pos2 = parametro.IndexOf("]]", pos1);
                if (pos2 >= 0) {
                    string clave64 = parametro.Substring(pos1, pos2 - pos1);
                    byte[] claveEncrip = Base64Decode(clave64);
                    string clave = Cadenas.Desencriptar(claveEncrip);
                    parametro = parametro.Substring(0, pos1 - 2) + clave + parametro.Substring(pos2 + 2);
                }
            }
            return parametro;
        }

        private string EncriptarPassword(string parametro) {
            int pos1 = parametro.ToUpper().IndexOf("PASSWORD=");
            if (pos1 >= 0 && parametro.Substring(pos1 + 9, 2) != "[[") {
                pos1 += 9;
                int pos2 = parametro.IndexOf(";", pos1);
                if (pos2 < 0) pos2 = parametro.Length;
                string clave = parametro.Substring(pos1, pos2 - pos1);
                parametro = String.Format("{0}[[{1}]]{2}", parametro.Substring(0, pos1), Cadenas.EncriptarToBase64(clave), parametro.Substring(pos2));
            }
            return parametro;
        }

        private List<string> KeysToList(ICollection Keys) {
            List<string> lista = new List<string>();
            foreach (string clave in this.Keys) lista.Add(clave);
            lista.Sort();
            return lista;
        }

        private void EncriptarParametros(XmlDocument xmlPar) {
            File.WriteAllBytes(PathAlmacen, Cadenas.Encriptar(xmlPar.OuterXml));
        }

        static private XmlDocument DesEncriptarParametros(string FicheroParametros) {
            byte[] buffer = File.ReadAllBytes(FicheroParametros);
            string xmlDoc = Cadenas.Desencriptar(buffer);

            // Crear XmlDocument.
            XmlDocument xmlPar = new XmlDocument();
            xmlPar.LoadXml(xmlDoc);
            return xmlPar;
        }

        private void ComprimirParametros(XmlDocument xmlPar) {
            FileStream outPar = File.OpenWrite(PathAlmacen);
            GZipStream gzip = new GZipStream(outPar, CompressionMode.Compress);
            //            BZip2Stream gzip = new BZip2Stream(outPar, CompressionMode.Compress);
            StreamWriter stWri = new StreamWriter(gzip);
            stWri.Write(xmlPar.OuterXml);
            stWri.Close();
            outPar.Close();
        }

        static private byte[] DesEncriptarFichero(string PathFichero, string sKey) {
            byte[] buffer = File.ReadAllBytes(PathFichero);
            return Cadenas.MiRijndael.Desencriptar(buffer, sKey);
        }

        static private Stream DesComprimir(byte[] bytDesComprimir) {
            MemoryStream inPar = new MemoryStream(bytDesComprimir);
            GZipStream rGzip = new GZipStream(inPar, CompressionMode.Decompress);
            //            BZip2Stream rGzip = new BZip2Stream(inPar, CompressionMode.Decompress);
            //            StreamReader zStream = new StreamReader(rGzip);

            return rGzip;
        }

        //static private byte[] DesComprimirFichero(string PathFichero)
        //{
        //    FileStream inPar = File.OpenRead(PathFichero);
        //    GZipStream rGzip = new GZipStream(inPar, CompressionMode.Decompress);
        //    //            BZip2Stream rGzip = new BZip2Stream(inPar, CompressionMode.Decompress);
        //    BinaryReader rBin = new BinaryReader(rGzip);
        //    byte[] buffer = rBin.ReadBytes(100000000);

        //    return buffer;
        //}

        static private XmlDocument DesComprimirParametros(string FicheroParametros) {
            FileStream inPar = File.OpenRead(FicheroParametros);
            GZipStream rGzip = new GZipStream(inPar, CompressionMode.Decompress);
            //            BZip2Stream rGzip = new BZip2Stream(inPar, CompressionMode.Decompress);
            StreamReader zStream = new StreamReader(rGzip);
            string xmlDoc = zStream.ReadToEnd();
            zStream.Dispose();

            // Crear XmlDocument.
            XmlDocument xmlPar = new XmlDocument();
            xmlPar.LoadXml(xmlDoc);
            return xmlPar;
        }

        private void ComprimirYEncriptarParametros(XmlDocument xmlPar) {
            MemoryStream outPar = new MemoryStream();
            GZipStream gzip = new GZipStream(outPar, CompressionMode.Compress);
            //            BZip2Stream gzip = new BZip2Stream(outPar, CompressionMode.Compress);
            StreamWriter stWri = new StreamWriter(gzip);
            stWri.Write(xmlPar.OuterXml);
            stWri.Close();
            outPar.Close();

            byte[] res = Cadenas.Encriptar(outPar.ToArray());
            File.WriteAllBytes(PathAlmacen, res);
        }

        //private void EncriptarYComprimirParametros(XmlDocument xmlPar)
        //{
        //    byte[] res = MiRijndael.Encriptar(xmlPar.OuterXml, "key");

        //    FileStream outPar = File.OpenWrite(PathAlmacen);
        //    GZipStream gzip = new GZipStream(outPar, CompressionMode.Compress);
        //    //            BZip2Stream gzip = new BZip2Stream(outPar, CompressionMode.Compress);
        //    BinaryWriter stWri = new BinaryWriter(gzip);
        //    foreach (byte b in res) stWri.Write(b);

        //    stWri.Close();
        //    outPar.Close();
        //}

        public void Guardar() {
            XmlDocument xmlPar = ParametrosHaciaXML();

            switch (Tipo) {
                case TipoAlmacen.Des:
                    EncriptarParametros(xmlPar);
                    break;
                case TipoAlmacen.ZDes:
                    ComprimirYEncriptarParametros(xmlPar);
                    break;
                case TipoAlmacen.Zip:
                    ComprimirParametros(xmlPar);
                    break;
                case TipoAlmacen.XML:
                    if (File.Exists(PathAlmacen)) File.Delete(PathAlmacen);
                    FileStream fPar = File.OpenWrite(PathAlmacen);
                    xmlPar.Save(fPar);
                    fPar.Close();
                    break;
            }
        }

        public void Guardar(TipoAlmacen tipoAlmacen) {
            this.Tipo = tipoAlmacen;
            Guardar();
        }

        public Parametros CargarParametros(string UbicacionParametros) {
            this.Clear();   // Limpiar colección de parámetros

            string NombreFichero = "";
            string UbicacionAbsoluta = "";
            XmlDocument xmlPar = null;
            if (File.Exists(UbicacionParametros)) {
                FileInfo FI = new FileInfo(UbicacionParametros);

                NombreFichero = FI.Name;
                UbicacionAbsoluta = FI.DirectoryName + "\\";
            }
            else {
                UbicacionParametros = (UbicacionParametros.Length == 0) ? @".\" : UbicacionParametros;
                string Directorio = UbicacionParametros.Substring(0, UbicacionParametros.LastIndexOf(@"\"));
                DirectoryInfo dir = new DirectoryInfo(Directorio);
                UbicacionAbsoluta = (dir.Exists) ? dir.FullName : "";
                UbicacionAbsoluta += (UbicacionAbsoluta.LastIndexOf("\\") == (UbicacionAbsoluta.Length - 1)) ? "" : "\\";

                if (File.Exists(UbicacionAbsoluta + "Parametros.des")) NombreFichero = "Parametros.des";
                else if (File.Exists(UbicacionAbsoluta + "Parametros.zdes")) NombreFichero = "Parametros.zdes";
                else if (File.Exists(UbicacionAbsoluta + "Parametros.zip")) NombreFichero = "Parametros.zip";
                else if (File.Exists(UbicacionAbsoluta + "Parametros.xml")) NombreFichero = "Parametros.xml";
            }
            if (NombreFichero.Length == 0) {
                NombreFichero = "Parametros.xml";
                xmlPar = new XmlDocument();
                xmlPar.InnerXml = "<?xml version=\"1.0\" encoding=\"utf-8\"?><Parametros></Parametros>";
                xmlPar.Save(UbicacionAbsoluta + NombreFichero);
            }
            switch (NombreFichero.ToLower()) {
                case "parametros.des":
                    xmlPar = DesEncriptarParametros(UbicacionAbsoluta + NombreFichero);
                    break;
                case "parametros.zdes":
                    Stream xmldoc = DesComprimir(DesEncriptarFichero(UbicacionAbsoluta + NombreFichero, "key"));
                    xmlPar = new XmlDocument();
                    xmlPar.Load(xmldoc);
                    break;
                case "parametros.zip":
                    string xmldocZ = DesComprimirParametros(UbicacionAbsoluta + NombreFichero).OuterXml;
                    xmlPar = new XmlDocument();
                    xmlPar.Load(xmldocZ);
                    break;
                case "parametros.xml":
                    FileStream fPar = File.OpenRead(UbicacionAbsoluta + "Parametros.xml");
                    xmlPar = new XmlDocument();
                    try {
                        xmlPar.Load(fPar);
                    }
                    catch (Exception ex) {
                        throw ex;
                    }
                    finally {
                        fPar.Close();
                    }
                    break;
            }
            this.PathAlmacen = UbicacionAbsoluta + NombreFichero;
            if (xmlPar != null) this.ParametrosDesdeXML(xmlPar);

            //this.vDataset = this.ParametrosADataset();

            return this;
        }

        // Define the event handlers. 
        private void watcher_Changed(object source, FileSystemEventArgs e) {
            if (e.ChangeType == WatcherChangeTypes.Changed || e.ChangeType == WatcherChangeTypes.Created)
                if (this.FileChanged != null) {
                    FileInfo FI = new FileInfo(((FileSystemEventArgs)e).FullPath);
                    if (FI.LastWriteTime.ToString() != this.FechaModificacion.ToString()) {
                        this.FechaModificacion = FI.LastWriteTime;
                        this.FileChanged(this, e);
                    }
                }
        }

        public event EventHandler FileChanged;


        //public static string Base64Encode(string plainText) {
        //    return Base64Encode(Encoding.UTF8.GetBytes(plainText));
        //}

        public static string Base64Encode(byte[] plainTextBytes) {
            return Convert.ToBase64String(plainTextBytes);
        }

        //public static string Base64DecodeToStr(string base64EncodedData) {
        //    var base64EncodedBytes = Convert.FromBase64String(base64EncodedData);
        //    return Encoding.UTF8.GetString(base64EncodedBytes);
        //}

        public static byte[] Base64Decode(string base64EncodedData) {
            return Convert.FromBase64String(base64EncodedData);
        }
    }
}