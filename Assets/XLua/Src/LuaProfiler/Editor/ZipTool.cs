/*     
* ==============================================================================
* Filename: ZipTool
* Created:  2016/7/4 12:05:50
* Author:   HaYaShi ToShiTaKa
* Purpose:  解压工具
* ==============================================================================
*/
using System;
using System.Collections.Generic;
using System.IO;
using ICSharpCode.SharpZipLib.Zip;

public class ZipTool {
    private static long m_allZipFileLength = 0;
    private static long m_zipedLength = 0;
    private static float m_process = 0;
    public static float process {
        get {
            return m_process;
        }
    }
    static public void ZipDirectory(string source, string target, int compressionLevel) {
        source.Replace("\\", "/");

        if (source.Length != 0) {
            // 打包所有的assetbundle到zip,先获取文件目录层级路径，以及assetbundle路径
            string[] pathDerctorys = Directory.GetDirectories(source, "*", SearchOption.AllDirectories);
            List<string> pathList = new List<string>(pathDerctorys);

            for (int i = 0; i < pathList.Count; ) {
                pathList[i] = pathList[i].Replace("\\", "/");
                if (Directory.GetFiles(pathList[i], "*", SearchOption.AllDirectories).Length != 0) {
                    pathList[i] = pathList[i] + "/";
                    i++;
                }
                else {
                    pathList.RemoveAt(i);
                }

            }
            pathDerctorys = pathList.ToArray();

            string[] pathFiles = Directory.GetFiles(source, "*", SearchOption.AllDirectories);

            string[] allPaths = ZipTool.MergerArray(pathDerctorys, pathFiles);
            int directoryNameLength = source.Length;

            for (int i = 0; i < allPaths.Length; i++) {
                allPaths[i] = allPaths[i].Replace("\\", "/");
            }
            m_process = 0;
            ZipTool.ZipDerctory(allPaths, target, directoryNameLength + 1, compressionLevel);
        }
    }
    static private string[] MergerArray(string[] First, string[] Second) {
        string[] result = new string[First.Length + Second.Length];
        First.CopyTo(result, 0);
        Second.CopyTo(result, First.Length);
        return result;
    }

    static private void ZipDerctory(string[] directoryToZip, string zipedDirectory, int fileLength, int compressionLevel) {
#if UNITY_EDITOR
        //UnityEditor.EditorUtility.ClearProgressBar();
#endif
        using (ZipOutputStream zipStream = new ZipOutputStream(File.Create(zipedDirectory))) {
            zipStream.SetLevel(compressionLevel);
            ZipEntry zipEntry = null;
            FileStream fileStream = null;
            for (int i = 0, imax = directoryToZip.Length; i < imax; i++) {
                string fileName = directoryToZip[i];
                zipEntry = new ZipEntry(fileName.Remove(0, fileLength));
                zipStream.PutNextEntry(zipEntry);
                if (!fileName.EndsWith(@"/")) {
                    fileStream = File.OpenRead(fileName);
                    byte[] buffer = new byte[fileStream.Length];
                    fileStream.Read(buffer, 0, buffer.Length);
                    zipStream.Write(buffer, 0, buffer.Length);
                    fileStream.Dispose();
                }
                m_process = i / (float)imax;
#if UNITY_EDITOR
                //UnityEditor.EditorUtility.DisplayProgressBar("ziping", string.Format("zip {0}", fileName), m_process);
#endif
            }
        }
#if UNITY_EDITOR
        //UnityEditor.EditorUtility.ClearProgressBar();
#endif
    }
    public static void UnZip(string zipedFile, string strDirectory, string password, bool overWrite, Action<string> endAction) {

        if (!strDirectory.EndsWith("/")) {
            strDirectory = strDirectory + "/";
        }

        using (ZipInputStream s = new ZipInputStream(File.OpenRead(zipedFile))) {
            s.Password = password;
            ZipEntry theEntry;
            m_allZipFileLength = s.Length;
            m_zipedLength = 0;
            m_process = 0;
            while ((theEntry = s.GetNextEntry()) != null) {
                UnzipOneFile(theEntry, s, strDirectory, overWrite);
            }
            s.Close();
        }
        if (endAction != null) {
            endAction(zipedFile);
        }
    }
    private static void UnzipOneFile(ZipEntry theEntry, ZipInputStream s, string strDirectory, bool overWrite) {
        string directoryName = "";
        string pathToZip = "";
        pathToZip = theEntry.Name;
        if (pathToZip != "")
            directoryName = Path.GetDirectoryName(pathToZip) + "/";

        string fileName = Path.GetFileName(pathToZip);

        Directory.CreateDirectory(strDirectory + directoryName);
        float rate = (float)theEntry.CompressedSize / (float)theEntry.Size;
        if (fileName != "") {
            if ((File.Exists(strDirectory + directoryName + fileName) && overWrite)
                || (!File.Exists(strDirectory + directoryName + fileName))) {
                using (FileStream streamWriter = File.Create(strDirectory + directoryName + fileName)) {
                    int size = 2048;
                    byte[] data = new byte[size];
                    while (true) {
                        size = s.Read(data, 0, data.Length);

                        if (size > 0) {
                            m_zipedLength += (long)(size * rate);
                            m_process = m_zipedLength / (float)m_allZipFileLength;
                            streamWriter.Write(data, 0, size);
                        }
                        else {
                            break;
                        }
                    }
                    streamWriter.Close();
                }
            }
        }

    }
}
