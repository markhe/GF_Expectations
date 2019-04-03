using System;
using System.IO;
using CsvHelper;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace GF_Expectations
{
    class Program
    {
        static Dictionary<Char, int> yOffset = new Dictionary<char, int>
        {
            {'A', 1}, {'B', 2}, {'C', 3 }, {'D', 4}, {'E', 5}, {'F', 6}, {'G', 7},
            {'H', 8}, {'I', 9}, {'J', 10}, {'K', 11}, {'L', 12}, {'M', 13}, {'N', 14},
            {'O', 15}, {'P', 16}, {'Q', 17}
        };
        static int RowCohortType = 3;
        static int RowCohort = 4;
        static int RowFirstData = 6;
        static int ColCohort = 4;
        static int ColRole = 1;
        static int ColCategory = 2;
        static int ColSubCategory = 3;


        static String[,] gfe = new String[100, 20];

        static void Main(string[] args)
        {
            String path = "AGFMapped.csv";
            Console.WriteLine("GF_Expectations commencing processing...");

            int rowCount = loadGfe(path);
            Console.WriteLine("\t{0} records processed.  Populating object model...", rowCount);
            Dictionary<String,GFCohort> gfeModel = popModel(rowCount);
            Console.WriteLine("\tObject model populated.  Serializing and saving model to AFG.json");
            saveModel(gfeModel);

            Console.WriteLine("Object model saved. Exiting program...");
        } // Main

        static Boolean saveModel(Dictionary<String, GFCohort> gfeModel)
        {
            Boolean rtn = true;

            string serializedModel = JsonConvert.SerializeObject(gfeModel);

            using (StreamWriter sw = new StreamWriter("AGF.json",false))
            {
                sw.Write(serializedModel);
            }

            return rtn;
        }

        static Dictionary<String, GFCohort> popModel(int rowCount)
        {
            int currentMaturityStageRow = 5;
            Dictionary<String, GFCohort> rtn = new Dictionary<string, GFCohort>();
            for (int col = ColCohort; col < 18; col++)
            {
                GFCohort cohort = new GFCohort();
                cohort.Role = new Dictionary<string, GFRole>();
                cohort.Cohort = gfe[RowCohort, col];
                cohort.Type = gfe[RowCohortType, col];

                int row = RowFirstData; 
                while(row < rowCount)
                {
                    GFRole role = new GFRole();
                    role.Categories = new Dictionary<string, GFCategory>();
                    role.Name = gfe[row, ColRole];
                    String currRole = role.Name;

                    while (currRole == gfe[row, ColRole])
                    {
                        GFCategory categ = new GFCategory();
                        categ.Expectations = new Dictionary<string, GFEx>();
                        categ.Name = gfe[row, ColCategory];
                        String currCategory = categ.Name;
                        while (currCategory == gfe[row, ColCategory])
                        {
                            GFEx gfex = new GFEx();
                            gfex.Description = new List<string>();
                            gfex.SubCategory = gfe[row, ColSubCategory];
                            gfex.MaturityStage = gfe[currentMaturityStageRow, col];
                            String currSubCateg = gfex.SubCategory;
                            while (currSubCateg == gfe[row, ColSubCategory])
                            {
                                gfex.Description.Add(gfe[row, col]);
                                row++;
                                while (String.IsNullOrWhiteSpace(gfe[row, ColRole]))    // Ignore empty lines
                                {
                                    row++;
                                    if (row >= rowCount) break;
                                }
                                if (row < rowCount && gfe[row, ColCategory].Contains("Maturity"))
                                {
                                    currentMaturityStageRow = row;
                                    row++;
                                }
                            }
                            categ.Expectations.Add(currSubCateg, gfex);
                        }
                        role.Categories.Add(currCategory, categ);
                    }
                    cohort.Role.Add(currRole, role);

                } // while

                rtn.Add(cohort.Cohort, cohort);
                if (cohort.Cohort == "CxO") break;  // Cluge - fix
            } // for
            return rtn;
        } // popModel

        static int loadGfe(String path)
        {
            //Console.WriteLine("Processing CSV file, {0}", path);
            int row = 0;
            int i = 0;
            Boolean isFirstRow = true;
            using (StreamReader rdr = new StreamReader(path))
            using (CsvReader csv = new CsvReader(rdr))
            {
                while (csv.Read() as dynamic)
                {
                    i = 0;
                    if (isFirstRow)
                    {
                        csv.ReadHeader();
                        isFirstRow = false;
                        //Console.WriteLine("Header Row = {0}", csv.Context.HeaderRecord);
                        foreach (string v in csv.Context.HeaderRecord)
                        {
                            gfe[row, i] = v;
                            i++;
                        }
                    }
                    else
                    {
                        //Console.WriteLine("Record {0} = {1} ", row, csv.Parser.FieldReader.Context.RawRecord);
                        for (int j = 0; j < csv.Parser.Context.RecordBuilder.Length; j++)
                        {
                            // if the field is empty fill it with the content of the previous field (if not the BOL)
                            if (String.IsNullOrEmpty(csv.GetField(j)))
                            {
                                if (j > 0)
                                {
                                    //Console.WriteLine("For cell ({0}, {1}) filling with value from previous cell", row, j);
                                    gfe[row, j] = gfe[row, j - 1];
                                }
                            }
                            // If the field contains a reference to another field (5:A, row:col) fill it with the content
                            //  of that field
                            else if (csv.GetField(j).Contains(':'))
                            {
                                String[] xy = csv.GetField(j).Split(':');
                                int x = int.Parse(xy[0]);
                                int y = yOffset.GetValueOrDefault(Convert.ToChar(xy[1].TrimEnd(' ')));
                                //Console.WriteLine("For cell ({0},{1}) filling with value from cell ({2}, {3})",row, j, x, y);
                                gfe[row, j] = gfe[x, y];
                            }
                            // For EOL identifier, if it's a blank line ignore it, otherwise populate the remaining
                            //      fields in the line with the contents from the current field.
                            else if (csv.GetField(j).Contains("X-"))
                            {
                                if (j < 4) break;
                                else
                                {
                                    for (int k = j; k < 18; k++)
                                    {
                                        gfe[row, k] = csv.GetField(j - 1);
                                    }
                                    break;
                                }
                            }
                            else if (csv.GetField(j).Contains("X"))
                                gfe[row, j] = "NA";
                            else
                                gfe[row, j] = csv.GetField(j);
                        }
                    }
                    row++;
                } // while
                //Console.WriteLine("Completed reading CSV.");
            } // using

            return row;
        }  // loadGfe
    } // class Program

    class GFCohort
    {
        public String Cohort { get; set; }
        public String Type { get; set; }
        public Dictionary<String, GFRole> Role { get; set; }

    } // class GFCohort

    class GFRole
    {
        public String Name { get; set; }
        public Dictionary<String, GFCategory> Categories { get; set; }
    }

    class GFCategory
    {
        public String Name { get; set; }
        public Dictionary<String, GFEx> Expectations { get; set; }
    }

    class GFEx
    {
        public String SubCategory { get; set; }
        public String MaturityStage { get; set; }
        public List<String> Description { get; set; }
    } // class GFEx
} // namespace