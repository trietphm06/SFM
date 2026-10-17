using UnityEngine;
using System.Collections.Generic;
using System;
using System.Globalization;
using System.Text;

namespace SFM.Core
{
    public class PlayerDatabase : MonoBehaviour
    {
        [Tooltip("File CSV xuất từ Excel")]
        public TextAsset CSVFile;

        public List<Player> AllPlayers = new List<Player>();

        void Awake()
        {
            if (CSVFile != null)
            {
                LoadPlayersFromCSV(CSVFile.text);
            }
        }

        public void LoadPlayersFromCSV(string csvData)
        {
            AllPlayers.Clear();
            
            string[] lines = csvData.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            
            // Dòng 0 là Header
            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i];
                string[] columns = ParseCsvLine(line).ToArray();

                if (columns.Length < 14) continue;

                try
                {
                    string name = columns[0].Trim();
                    string pos = columns[1].Trim().ToUpper();

                    // New format: Name,Position,AltPos,Year,Season,Pace,...,IQ,Stamina
                    // Legacy format: Name,Position,Year,Season,Pace,...,IQ,Stamina
                    bool hasAltPos = columns.Length >= 19;
                    bool hasMetadata = columns.Length >= 18;
                    var altPositions = new List<string>();
                    if (hasAltPos)
                    {
                        foreach (var a in columns[2].Split(','))
                        {
                            string alt = a.Trim().ToUpper();
                            if (alt.Length > 0) altPositions.Add(alt);
                        }
                    }
                    string year = hasAltPos ? columns[3].Trim() : (hasMetadata ? columns[2].Trim() : "2023");
                    string season = hasAltPos ? columns[4].Trim() : (hasMetadata ? columns[3].Trim() : "Base");
                    int statOffset = hasAltPos ? 5 : (hasMetadata ? 4 : 2);

                    float pace = ParseFloat(columns[statOffset]);
                    float shooting = ParseFloat(columns[statOffset + 1]);
                    float passing = ParseFloat(columns[statOffset + 2]);
                    float dribbling = ParseFloat(columns[statOffset + 3]);
                    float defending = ParseFloat(columns[statOffset + 4]);
                    float physical = ParseFloat(columns[statOffset + 5]);

                    float diving = ParseFloat(columns[statOffset + 6]);
                    float handling = ParseFloat(columns[statOffset + 7]);
                    float kicking = ParseFloat(columns[statOffset + 8]);
                    float reflexing = ParseFloat(columns[statOffset + 9]);
                    float speed = ParseFloat(columns[statOffset + 10]);
                    float positioning = ParseFloat(columns[statOffset + 11]);

                    float iq = hasMetadata ? ParseFloat(columns[statOffset + 12]) : 70f;
                    float sta = hasMetadata ? ParseFloat(columns[statOffset + 13]) : (pos == "GK" ? 70f : physical);

                    if (pos == "GK")
                    {
                        var gk = new Goalkeeper
                        {
                            Name = name,
                            Position = pos,
                            AltPositions = altPositions,
                            Year = year,
                            Season = season,
                            BaseDiving = diving,
                            BaseHandling = handling,
                            BaseKicking = kicking,
                            BaseReflexing = reflexing,
                            BaseSpeed = speed,
                            BasePositioning = positioning,
                            BasePhysical = physical > 0 ? physical : 50f,
                            IQ = iq,
                            MaxStamina = sta,
                            Stamina = 100f
                        };
                        AllPlayers.Add(gk);
                    }
                    else
                    {
                        var outfield = new OutfieldPlayer
                        {
                            Name = name,
                            Position = pos,
                            AltPositions = altPositions,
                            Year = year,
                            Season = season,
                            BasePace = pace,
                            BaseShooting = shooting,
                            BasePassing = passing,
                            BaseDribbling = dribbling,
                            BaseDefending = defending,
                            BasePhysical = physical,
                            IQ = iq,
                            MaxStamina = sta,
                            Stamina = 100f
                        };
                        AllPlayers.Add(outfield);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"Lỗi đọc dữ liệu dòng {i}: {line} \nLỗi: {e.Message}");
                }
            }
            
            Debug.Log($"Đã nạp thành công {AllPlayers.Count} cầu thủ từ Database.");
        }

        private static float ParseFloat(string value)
        {
            if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed))
            {
                return parsed;
            }

            return 0f;
        }

        private static List<string> ParseCsvLine(string line)
        {
            var values = new List<string>();
            var value = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char current = line[i];
                if (current == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        value.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if ((current == ',' || current == ';' || current == '\t') && !inQuotes)
                {
                    values.Add(value.ToString().Trim());
                    value.Clear();
                }
                else
                {
                    value.Append(current);
                }
            }

            values.Add(value.ToString().Trim());
            return values;
        }
    }
}
