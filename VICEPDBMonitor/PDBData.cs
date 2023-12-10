﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Globalization;

namespace VICEPDBMonitor
{
    public class AddrInfo
    {
        public int mAddr = -1;
        public int mPrevAddr = -1;
        public int mNextAddr = -1;
        public int mZone = -1;
        public int mBaseZone = -1;
        public int mFile = -1;
        public int mLine = -1;
        public int mDevice = 0;
        public ContextDataSource mContext = null;

        public AddrInfo Clone()
        {
            return (AddrInfo) this.MemberwiseClone();
        }
    }

    public class LabelInfo
    {
        public int mAddr;
        public int mZone;
        public string mLabel;
        public bool mUsed;
        public bool mMemory;
        public int mDevice;
    }

    public class PDBData
    {
        private static PDBData g_PDBData = null;

        List<string> mSourceIncludes = new List<string>();
        string[] mSourceFileNames = null;
        int mSourceFileNamesLength = 0;
        List<string> mSourceFileNamesFound = new List<string>();
        List<List<string>> mSourceFiles = new List<List<string>>();
        List<LabelInfo> mAllLabels = new List<LabelInfo>();

        Dictionary<int, Dictionary<int, SortedDictionary<int, AddrInfo>>> mAddrInfoByAddrByZoneByDevice = new Dictionary<int, Dictionary<int, SortedDictionary<int, AddrInfo>>>();
        SortedDictionary<int, AddrInfo> mAddrInfoByAddr = new SortedDictionary<int, AddrInfo>();
//        MultiMap<int, LabelInfo> mLabelInfoByAddr = new MultiMap<int, LabelInfo>();
        MultiMap<int, LabelInfo> mLabelInfoByZone = new MultiMap<int, LabelInfo>();
//        MultiMap<string, LabelInfo> mLabelInfoByLabel = new MultiMap<string, LabelInfo>();

        public void refreshContextListForAddress(int address)
        {
            MainWindow.mContextList.Clear();
            SortedSet<string> set = new SortedSet<string>();

            Dictionary<int, SortedDictionary<int, AddrInfo>> addrInfoByAddrByZone = null;
            Dictionary<int, SortedDictionary<int, AddrInfo>> addrInfoByAddrByZone2 = null;
            mAddrInfoByAddrByZoneByDevice.TryGetValue(0x00, out addrInfoByAddrByZone);
            if (MainWindow.mIsAPUMode)
            {
                mAddrInfoByAddrByZoneByDevice.TryGetValue(0x40, out addrInfoByAddrByZone2);
                if (addrInfoByAddrByZone2 != null)
                {
                    addrInfoByAddrByZone = addrInfoByAddrByZone2;
                }
            }
            if (MainWindow.mIsDriveMode)
            {
                mAddrInfoByAddrByZoneByDevice.TryGetValue(0x08, out addrInfoByAddrByZone2);
                if (addrInfoByAddrByZone2 != null)
                {
                    addrInfoByAddrByZone = addrInfoByAddrByZone2;
                }
            }

            if (addrInfoByAddrByZone != null)
            {
                foreach (KeyValuePair<int, SortedDictionary<int, AddrInfo>> addrInfoByAddr in addrInfoByAddrByZone)
                {
                    AddrInfo addrInfo = null;
                    addrInfoByAddr.Value.TryGetValue(address, out addrInfo);
                    if (addrInfo != null)
                    {
                        if (addrInfo.mContext == null)
                        {
                            addrInfo.mContext = new ContextDataSource();
                            addrInfo.mContext.Source = mSourceFileNames[addrInfo.mFile];
                            addrInfo.mContext.Device = addrInfo.mDevice.ToString();
                            addrInfo.mContext.Zone = addrInfo.mZone.ToString();
                            addrInfo.mContext.Enable = false;
                            addrInfo.mContext.previousEnable = addrInfo.mContext.Enable;
                        }
                        if (set.Add(addrInfo.mContext.getKey()))
                        {
                            MainWindow.mContextList.Add(addrInfo.mContext);
                        }
                    }
                }
            }
        }

        int mAPUCode_Start = 0;
        int mDriveCode_Start = 0;
        int mDriveCode_StartReal = 0;

        public static PDBData getInstance() { return g_PDBData; }

        public static PDBData create(string[] commandLineArgs)
        {
            g_PDBData = new PDBData();
            g_PDBData.parseData(commandLineArgs);
            return g_PDBData;
        }

        public void parseData(string[] commandLineArgs)
        {
            int i;
            string line;
            for (i = 1; i < commandLineArgs.Length; i++)
            {
                int localFileIndex = 0;

                // Read the file and parse it line by line.
                using (System.IO.StreamReader file = new System.IO.StreamReader(commandLineArgs[i]))
                {
                    int baseZone = 0;
                    if (mLabelInfoByZone.Count > 0)
                    {
                        baseZone = mLabelInfoByZone.Keys.Max() + 1;
                    }

                    while ((line = file.ReadLine()) != null)
                    {
                        if (line.IndexOf("INCLUDES:") == 0)
                        {
                            int lines = int.Parse(line.Substring(9));
                            mSourceIncludes.Clear();
                            mSourceIncludes.Add(".\\");
                            while (lines-- > 0)
                            {
                                line = file.ReadLine();
                                mSourceIncludes.Add(line);
                            }
                        }
                        else if (line.IndexOf("FILES:") == 0)
                        {
                            localFileIndex = mSourceFileNamesLength;
                            int lines = int.Parse(line.Substring(6));
                            mSourceFileNamesLength += lines;
                            if (mSourceFileNames != null)
                            {
                                // Copy old into new
                                string[] tempNames = new string[mSourceFileNamesLength];
                                int j;
                                for (j = 0; j < localFileIndex; j++)
                                {
                                    tempNames[j] = mSourceFileNames[j];
                                }
                                mSourceFileNames = tempNames;
                            }
                            else
                            {
                                mSourceFileNames = new string[mSourceFileNamesLength];
                            }
                            while (lines-- > 0)
                            {
                                line = file.ReadLine();

                                Char[] separator = { ':' };
                                string[] tokens = line.Split(separator, 2);
                                mSourceFileNames[localFileIndex + int.Parse(tokens[0])] = tokens[1];
                            }
                        }
                        else if (line.IndexOf("ADDRS:") == 0)
                        {
                            int lines = int.Parse(line.Substring(6));
                            while (lines-- > 0)
                            {
                                line = file.ReadLine();
                                string[] tokens = line.Split(':');
                                AddrInfo addrInfo = new AddrInfo();
                                addrInfo.mAddr = int.Parse(tokens[0].Substring(1), NumberStyles.HexNumber);
                                addrInfo.mZone = int.Parse(tokens[1]);
                                if (addrInfo.mZone > 0)
                                {
                                    addrInfo.mZone += baseZone;
                                }
                                addrInfo.mBaseZone = baseZone;
                                addrInfo.mFile = localFileIndex + int.Parse(tokens[2]);
                                addrInfo.mLine = int.Parse(tokens[3]) - 1;  // Files lines are 1 based in the debug file
                                if (tokens.Length >= 5)
                                {
                                    addrInfo.mDevice = int.Parse(tokens[4]);
                                }

                                mAddrInfoByAddr[addrInfo.mAddr] = addrInfo;

                                // There has to be a better way to create default entries if they don't exist...
                                Dictionary<int, SortedDictionary<int, AddrInfo>> addrInfoByAddrByZone;
                                if (!mAddrInfoByAddrByZoneByDevice.TryGetValue(addrInfo.mDevice, out addrInfoByAddrByZone))
                                {
                                    addrInfoByAddrByZone = new Dictionary<int, SortedDictionary<int, AddrInfo>>();
                                    mAddrInfoByAddrByZoneByDevice[addrInfo.mDevice] = addrInfoByAddrByZone;
                                }
                                SortedDictionary<int, AddrInfo> addrInfoByAddr;
                                if (!addrInfoByAddrByZone.TryGetValue(addrInfo.mZone, out addrInfoByAddr))
                                {
                                    addrInfoByAddr = new SortedDictionary<int, AddrInfo>();
                                    addrInfoByAddrByZone[addrInfo.mZone] = addrInfoByAddr;
                                }
                                addrInfoByAddr[addrInfo.mAddr] = addrInfo;
                            }
                        }
                        else if (line.IndexOf("LABELS:") == 0)
                        {
                            int lines = int.Parse(line.Substring(7));
                            while (lines-- > 0)
                            {
                                line = file.ReadLine();
                                string[] tokens = line.Split(':');
                                LabelInfo labelInfo = new LabelInfo();
                                labelInfo.mAddr = int.Parse(tokens[0].Substring(1), NumberStyles.HexNumber);
                                labelInfo.mZone = int.Parse(tokens[1]);
                                if (labelInfo.mZone > 0)
                                {
                                    labelInfo.mZone += baseZone;    // Helps to distinguish zones for multiple PDB files
                                }
                                labelInfo.mLabel = tokens[2];
                                labelInfo.mUsed = int.Parse(tokens[3]) == 1;
                                labelInfo.mMemory = int.Parse(tokens[4]) == 1;
                                labelInfo.mDevice = 0;
                                if (tokens.Length >= 6)
                                {
                                    labelInfo.mDevice = int.Parse(tokens[5]);
                                }

                                if (labelInfo.mLabel.Equals("APUCode_Start"))
                                {
                                    mAPUCode_Start = labelInfo.mAddr;
                                }
                                else if (labelInfo.mLabel.Equals("DriveCode_Start"))
                                {
                                    mDriveCode_Start = labelInfo.mAddr;
                                }
                                else if (labelInfo.mLabel.Equals("DriveCode_StartReal"))
                                {
                                    mDriveCode_StartReal = labelInfo.mAddr;
                                }
                                mAllLabels.Add(labelInfo);
//                                mLabelInfoByAddr.Add(labelInfo.mAddr, labelInfo);
                                mLabelInfoByZone.Add(labelInfo.mZone, labelInfo);
//                                mLabelInfoByLabel.Add(labelInfo.mLabel, labelInfo);
                            }
                        }
                    }

                    mAllLabels.Sort((a, b) => b.mLabel.Length.CompareTo(a.mLabel.Length));

                    file.Close();
                }
                int l;
                // Only process new names this iteration
                // Use mSourceIncludes
                for (l = localFileIndex; l < mSourceFileNamesLength; l++)
                {
                    string name = mSourceFileNames[l];
                    try
                    {
                        List<string> aFile = new List<string>();
                        string newPath = name;
                        if (!System.IO.File.Exists(newPath))
                        {
                            foreach (string prefix in mSourceIncludes)
                            {
                                string newName = System.IO.Path.Combine(prefix, name);
                                newPath = newName;
                                if (!System.IO.File.Exists(newPath))
                                {
                                    newPath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(commandLineArgs[i]), newName);
                                    if (!System.IO.File.Exists(newPath))
                                    {
                                        newPath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(commandLineArgs[i]), newName);
                                        if (!System.IO.File.Exists(newPath))
                                        {
                                            newPath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(commandLineArgs[i]), System.IO.Path.GetFileName(newName));
                                            if (!System.IO.File.Exists(newPath))
                                            {
                                                newPath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(commandLineArgs[i]) + "..\\", System.IO.Path.GetFileName(newName));
                                            }
                                        }
                                    }
                                }

                                if (System.IO.File.Exists(newPath))
                                {
                                    break;
                                }
                            }
                        }
                        using (System.IO.StreamReader file = new System.IO.StreamReader(newPath))
                        {
                            while ((line = file.ReadLine()) != null)
                            {
                                aFile.Add(line);
                            }
                            file.Close();
                        }
                        mSourceFiles.Add(aFile);
                        mSourceFileNamesFound.Add(newPath);
                    }
                    catch (System.Exception)
                    {
                        mSourceFiles.Add(new List<string>());
                        mSourceFileNamesFound.Add("");
                    }
                }

            }

            // Link the AddrInfo by their respective device and then zone
            int thePrevAddr = -1;
            foreach (KeyValuePair<int, Dictionary<int, SortedDictionary<int, AddrInfo>>> pair1 in mAddrInfoByAddrByZoneByDevice)
            {
                Dictionary<int, SortedDictionary<int, AddrInfo>> byZone = pair1.Value;
                foreach (KeyValuePair<int, SortedDictionary<int, AddrInfo>> pair2 in byZone)
                {
                    SortedDictionary<int, AddrInfo> byAddr = pair2.Value;
                    foreach (KeyValuePair<int, AddrInfo> pair in byAddr)
                    {
                        pair.Value.mPrevAddr = thePrevAddr;
                        thePrevAddr = pair.Value.mAddr;
                    }
                    thePrevAddr = -1;
                    foreach (KeyValuePair<int, AddrInfo> pair in byAddr.Reverse())
                    {
                        pair.Value.mNextAddr = thePrevAddr;
                        thePrevAddr = pair.Value.mAddr;
                    }
                }
            }


            thePrevAddr = -1;
            foreach (KeyValuePair<int, AddrInfo> pair in mAddrInfoByAddr)
            {
                pair.Value.mPrevAddr = thePrevAddr;
                thePrevAddr = pair.Value.mAddr;
            }
            thePrevAddr = -1;
            foreach (KeyValuePair<int, AddrInfo> pair in mAddrInfoByAddr.Reverse())
            {
                pair.Value.mNextAddr = thePrevAddr;
                thePrevAddr = pair.Value.mAddr;
            }
        }

        public AddrInfo getAddrInfoForAddr(int PC)
        {
            if (MainWindow.mIsAPUMode)
            {
                AddrInfo tweakedInfo = mAddrInfoByAddr[(PC*4) + mAPUCode_Start].Clone();
                tweakedInfo.mAddr -= mAPUCode_Start;
                tweakedInfo.mAddr /= 4;
                tweakedInfo.mNextAddr = tweakedInfo.mAddr + 1;
                tweakedInfo.mPrevAddr = tweakedInfo.mAddr - 1;
                if (tweakedInfo.mPrevAddr < 0)
                {
                    tweakedInfo.mPrevAddr = 0;
                }
                return tweakedInfo;
            }
            if (MainWindow.mIsDriveMode)
            {
                int delta = -mDriveCode_Start + mDriveCode_StartReal;
                AddrInfo tweakedInfo = mAddrInfoByAddr[PC - delta].Clone();
                tweakedInfo.mAddr += delta;
                tweakedInfo.mNextAddr += delta;
                tweakedInfo.mPrevAddr += delta;
                if (tweakedInfo.mPrevAddr < 0)
                {
                    tweakedInfo.mPrevAddr = 0;
                }
                return tweakedInfo;
            }
            return mAddrInfoByAddr[PC];
        }

        public List<LabelInfo> getLabelsForZone(int zone)
        {
            return mLabelInfoByZone[zone];
        }

        public int getNumFiles()
        {
            return mSourceFileNamesLength;
        }

        public string getSourceFileName(int num)
        {
            return mSourceFileNames[num];
        }

        public string getLineFromSourceFile(int file, int line)
        {
            return mSourceFiles[file][line];
        }

        public List<LabelInfo> getAllLabels()
        {
            return mAllLabels;
        }
    }
}
