﻿using ARKViewer.Configuration;
using ASVPack;
using ASVPack.Models;
using FluentFTP;
using Newtonsoft.Json;
using Renci.SshNet;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.ListView;

namespace ARKViewer.Models
{
    public class ASVDataManager
    {


        //cached image params
        ASVStructureOptions cachedOptions = new ASVStructureOptions();
        string lastRealm = string.Empty;
        Tuple<long, bool, bool, bool> cacheImageTribes = null;
        Tuple<string, string, int, int, float, float, float> cacheImageWild = null;
        Tuple<string, string, bool, long, long> cacheImageTamed = null;
        Tuple<long, string> cacheImageDroppedItems = null;
        Tuple<long> cacheImageDropBags = null;
        Tuple<string, long, long> cacheImagePlayerStructures = null;
        Tuple<long, long> cacheImagePlayers = null;
        Tuple<long, string, decimal, decimal> cacheImageItems = null;

        string lastDrawRequest = "";
        Image gameContentMap = null; //wilds/tames/tribes/players etc.

        ContentPack pack = null;
        public ContentMap LoadedMap { get; internal set; } = null;
        

        public DateTime ContentDate
        {
            get
            {
                if (pack == null) return DateTime.MinValue;
                return pack.ContentDate;
                
            }
        }

        public string MapName
        {
            get
            {
                string returnName = "Unknown Map";
                returnName = Program.MapPack.SupportedMaps.FirstOrDefault(m => m.Filename.ToLower().Contains(MapFilename.ToLower()))?.MapName;
                return returnName;
            }
        }

        public string MapFilename
        {
            get
            {
                if (pack == null) return "";
                return pack.MapFilename;
            }
        }

        public string Realm { get; set; } = "";
        


        public Image MapImage
        {
            get
            {
                

                string imageFilePath = AppContext.BaseDirectory;
                string imageFilename = Program.MapPack.SupportedMaps.FirstOrDefault(m => m.Filename.ToLower().Contains(MapFilename.ToLower()))?.ImageFile;
                if(Realm!=null && Realm.Length > 0)
                {
                    var selectedRealm = LoadedMap.Regions.FirstOrDefault(r => r.RegionName == Realm);
                    if (selectedRealm != null)
                    {
                        var realmImage = selectedRealm.ImageFile ?? "";
                        if(realmImage.Length > 0)
                        {
                            var realmFilePath = Path.Combine(imageFilePath, "Maps\\", realmImage);
                            if (File.Exists(realmFilePath))
                            {
                                imageFilename = realmImage;
                            }
                        }

                    }
                }

                try
                {
                    return Image.FromFile(Path.Combine(imageFilePath, "Maps\\", imageFilename));
                }
                catch 
                {
                    return new Bitmap(1024, 1024);
                }
                
            }

        }
        public bool MapTerminals { get; set; } = true;
        public bool MapOilVeins { get; set; } = true;
        public bool MapGasVeins { get; set; } = true;
        public bool MapWaterVeins { get; set; } = true;
        public bool MapChargeNodes { get; set; } = true;
        public bool MapArtifacts { get; set; } = true;
        public bool MapWyvernNests { get; set; } = true;

        public bool MapDeinoNests { get; set; } = true;
        public bool MapDrakeNests { get; set; } = true;
        public bool MapMagmaNests { get; set; } = true;
        public bool MapBeaverDams { get; set; } = true;
        public bool MapGlitches { get; set; } = true;

        public ASVDataManager(ContentContainer data)
        {
            pack = new ContentPack(data, 0, 0, 50, 50, 100,true,true,true,true,true,true,true);
            if(data.MapStructures.LongCount(x => x.ClassName == "ASV_Glitch") > 0)
            {
                data.MapStructures.Where(x=>x.ClassName == "ASV_Glitch")
                    .ToList()
                    .ForEach(x =>
                    {
                        pack.GlitchMarkers.Add(x);
                    });
            }

            LoadedMap = new ContentMapPack().GetMap($"{pack.MapFilename}.ark");

        }

        public ASVDataManager(ContentPack data)
        {
            pack = data;
        }

        ~ASVDataManager()
        {
            pack = null;
        }


        //Query options
        public List<ContentWildCreature> GetWildCreatures(int minLevel, int maxLevel, float fromLat, float fromLon, float fromRadius, string selectedClass, string selectedRealm)
        {
            if (pack.WildCreatures == null) return new List<ContentWildCreature>();

            Realm = selectedRealm;

            var wilds = pack.WildCreatures.Where(w =>
                                            ((w.ClassName == selectedClass || selectedClass == "") && ((w.BaseLevel >= minLevel && w.BaseLevel <= maxLevel) || w.BaseLevel == 0))
                                            && 
                                            (
                                                (Math.Abs(w.Latitude.GetValueOrDefault(0) - fromLat) <= fromRadius)
                                                && (Math.Abs(w.Longitude.GetValueOrDefault(0) - fromLon) <= fromRadius)
                                            )
                                            
                                ).OrderByDescending(c => c.BaseLevel).ToList();


            if (!string.IsNullOrEmpty(selectedRealm))
            {
                if(LoadedMap.Regions!=null && LoadedMap.Regions.Count > 0)
                {
                    var selectedRegion = LoadedMap.Regions.FirstOrDefault(r=>r.RegionName == selectedRealm);
                    wilds.RemoveAll(wild => !(                    
                            wild.Z >= selectedRegion.ZStart
                            && wild.Z <= selectedRegion.ZEnd
                            && wild.Latitude >= selectedRegion.LatitudeStart
                            && wild.Latitude <= selectedRegion.LatitudeEnd
                            && wild.Longitude >= selectedRegion.LongitudeStart
                            && wild.Longitude <= selectedRegion.LongitudeEnd
                            )
                    );

                }

            }


            return wilds;
        }

        public List<ContentTamedCreature> GetTamedCreatures(string selectedClass, long selectedTribeId, long selectedPlayerId, bool includeCryoVivarium, string selectedRealm)
        {
            if (pack.Tribes == null) return new List<ContentTamedCreature>();

            Realm = selectedRealm;

            var tamed = pack.Tribes
                .Where(t => (t.TribeId == selectedTribeId || selectedTribeId == 0) || t.Players.Any(p => p.Id == selectedPlayerId && selectedPlayerId != 0))
                .SelectMany(c =>
                                c.Tames.Where(w =>
                                    (w.ClassName == selectedClass || selectedClass == "")
                                    & !(w.ClassName == "MotorRaft_BP_C" || w.ClassName == "Raft_BP_C")
                                    && (includeCryoVivarium || w.IsCryo == false)
                                    && (includeCryoVivarium || w.IsVivarium == false)
                                )
                            ).ToList();

            if (!string.IsNullOrEmpty(selectedRealm))
            {
                if (LoadedMap.Regions != null && LoadedMap.Regions.Count > 0)
                {
                    var selectedRegion = LoadedMap.Regions.FirstOrDefault(r => r.RegionName == selectedRealm);
                    tamed.RemoveAll(tame => !(
                            tame.Z >= selectedRegion.ZStart
                            && tame.Z <= selectedRegion.ZEnd
                            && tame.Latitude >= selectedRegion.LatitudeStart
                            && tame.Latitude <= selectedRegion.LatitudeEnd
                            && tame.Longitude >= selectedRegion.LongitudeStart
                            && tame.Longitude <= selectedRegion.LongitudeEnd
                            )
                    );

                }

            }

            return tamed;

        }

        public ContentTamedCreature GetTamedCreature(long tameId)
        {
            if (pack.Tribes == null) return null;
            return pack.Tribes
                .SelectMany(c =>
                                c.Tames.Where(w => (long)w.Id == tameId)
                            ).ToList().FirstOrDefault();
        }

        public List<ContentStructure> GetTerminals()
        {
            if (pack == null || pack.TerminalMarkers == null) return new List<ContentStructure>();
            return pack.TerminalMarkers;
        }

        public List<ContentStructure> GetGlitchMarkers()
        {
            if (pack == null || pack.GlitchMarkers == null) return new List<ContentStructure>();
            return pack.GlitchMarkers;
        }

        public List<ContentStructure> GetChargeNodes()
        {
            if (pack == null || pack.ChargeNodes == null) return new List<ContentStructure>();
            return pack.ChargeNodes;
        }

        public List<ContentStructure> GetBeaverDams()
        {
            if (pack == null || pack.BeaverDams == null) return new List<ContentStructure>();
            return pack.BeaverDams;
        }

        public List<ContentStructure> GetBeeHives()
        {
            if (pack == null || pack.BeeHives == null) return new List<ContentStructure>();
            return pack.BeeHives;
        }

        public List<ContentStructure> GetWyvernNests()
        {
            if (pack == null || pack.WyvernNests == null) return new List<ContentStructure>();
            return pack.WyvernNests;
        }

        public List<ContentStructure> GetDrakeNests()
        {
            if (pack == null || pack.DrakeNests == null) return new List<ContentStructure>();
            return pack.DrakeNests;
        }

        public List<ContentStructure> GetDeinoNests()
        {
            if (pack == null || pack.DeinoNests == null) return new List<ContentStructure>();
            return pack.DeinoNests;
        }

        public List<ContentStructure> GetMagmaNests()
        {
            if (pack == null || pack.MagmaNests == null) return new List<ContentStructure>();
            return pack.MagmaNests;
        }

        public List<ContentStructure> GetOilVeins()
        {
            if (pack == null || pack.OilVeins == null) return new List<ContentStructure>();
            return pack.OilVeins;
        }

        public List<ContentStructure> GetWaterVeins()
        {
            if (pack == null || pack.WaterVeins == null) return new List<ContentStructure>();
            return pack.WaterVeins;
        }

        public List<ContentStructure> GetGasVeins()
        {
            if (pack == null || pack.GasVeins == null) return new List<ContentStructure>();
            return pack.GasVeins;
        }

        public List<ContentStructure> GetArtifacts()
        {
            if (pack == null || pack.Artifacts == null) return new List<ContentStructure>();
            return pack.Artifacts;
        }

        public ContentPack GetPack()
        {
            return pack;
        }

        public List<ContentStructure> GetPlantZ()
        {
            if (pack == null || pack.PlantZ == null) return new List<ContentStructure>();
            return pack.PlantZ;


        }

        public List<ContentStructure> GetPlayerStructures(long selectedTribeId, long selectedPlayerId, string selectedClass, bool includeExcluded, string selectedRealm)
        {
            if (pack.Tribes == null) return new List<ContentStructure>();

            Realm = selectedRealm;

            var tribeStructures = pack.Tribes
                .Where(t =>
                    (t.TribeId == selectedTribeId || (selectedTribeId == 0 && selectedPlayerId == 0))
                    || t.Players.Any(p => p.Id == selectedPlayerId)
                    && t.Structures != null
                ).SelectMany(s =>
                    s.Structures.Where(x =>
                        (selectedClass.Length == 0 || x.ClassName == selectedClass)
                        &&
                        (!Program.ProgramConfig.StructureExclusions.Contains(x.ClassName) || includeExcluded)
                    )
                ).ToList();

            if (!string.IsNullOrEmpty(selectedRealm))
            {
                if (LoadedMap.Regions != null && LoadedMap.Regions.Count > 0)
                {
                    var selectedRegion = LoadedMap.Regions.FirstOrDefault(r => r.RegionName == selectedRealm);
                    tribeStructures.RemoveAll(playerStructure => !(
                            playerStructure.Z >= selectedRegion.ZStart
                            && playerStructure.Z <= selectedRegion.ZEnd
                            && playerStructure.Latitude >= selectedRegion.LatitudeStart
                            && playerStructure.Latitude <= selectedRegion.LatitudeEnd
                            && playerStructure.Longitude >= selectedRegion.LongitudeStart
                            && playerStructure.Longitude <= selectedRegion.LongitudeEnd
                            )
                    );

                }

            }
            
            return tribeStructures;
        }

        public List<ContentInventory> GetInventories()
        {
            var returnList = new ConcurrentBag<ContentInventory>();
            Parallel.ForEach(pack.Tribes, t =>
            {
                t.Structures.ToList().ForEach(s => returnList.Add(s.Inventory));
                t.Tames.ToList().ForEach(s => returnList.Add(s.Inventory));
                t.Players.ToList().ForEach(s => returnList.Add(s.Inventory));
            });

            return returnList.ToList();
        }

        public List<ContentPlayer> GetPlayers(long selectedTribeId, long selectedPlayerId, string selectedRealm)
        {
            if (pack.Tribes == null) return new List<ContentPlayer>();

            Realm = selectedRealm;

            var tribePlayers = pack.Tribes
                .Where(t =>
                    t.TribeId == selectedTribeId || selectedTribeId == 0
                ).SelectMany(s =>
                    s.Players.Where(p =>
                        (selectedPlayerId == 0 || p.Id == selectedPlayerId)                        
                    )
                ).ToList();

            if (!string.IsNullOrEmpty(selectedRealm))
            {
                if (LoadedMap.Regions != null && LoadedMap.Regions.Count > 0)
                {
                    var selectedRegion = LoadedMap.Regions.FirstOrDefault(r => r.RegionName == selectedRealm);
                    tribePlayers.RemoveAll(player => !(
                            player.Z >= selectedRegion.ZStart
                            && player.Z <= selectedRegion.ZEnd
                            && player.Latitude >= selectedRegion.LatitudeStart
                            && player.Latitude <= selectedRegion.LatitudeEnd
                            && player.Longitude >= selectedRegion.LongitudeStart
                            && player.Longitude <= selectedRegion.LongitudeEnd
                            )
                    );

                }

            }

            return tribePlayers;
        }

        public List<ContentTribe> GetTribes(long selectedTribeId)
        {
            if (pack.Tribes == null) return new List<ContentTribe>();
            return pack.Tribes.Where(t => (selectedTribeId == 0 || t.TribeId == selectedTribeId) &! string.IsNullOrEmpty(t.TribeName)).ToList();
        }

        public ContentTribe GetPlayerTribe(long playerId)
        {
            if (pack.Tribes == null) return new ContentTribe()
            {
                TribeId = playerId,
                TribeName = "N/a"
            };
            return pack.Tribes.FirstOrDefault<ContentTribe>(t => t.Players.Any(p => p.Id == playerId));
        }

        public List<ContentDroppedItem> GetDroppedItems(long playerId, string className, string selectedRealm)
        {
            if (pack.DroppedItems == null) return new List<ContentDroppedItem>();

            Realm = selectedRealm;

            var foundItems = pack.DroppedItems
                .Where(d =>
                    d.IsDeathCache == false
                    && ((d.DroppedByPlayerId == playerId && playerId > 0) || (playerId == 0 && d.DroppedByPlayerId > 0) || (playerId < 0 && d.DroppedByPlayerId == 0))
                    && (d.ClassName == className || className == "")
                ).ToList();

            if (!string.IsNullOrEmpty(selectedRealm))
            {
                if (LoadedMap.Regions != null && LoadedMap.Regions.Count > 0)
                {
                    var selectedRegion = LoadedMap.Regions.FirstOrDefault(r => r.RegionName == selectedRealm);
                    foundItems.RemoveAll(foundItem => !(
                            foundItem.Z >= selectedRegion.ZStart
                            && foundItem.Z <= selectedRegion.ZEnd
                            && foundItem.Latitude >= selectedRegion.LatitudeStart
                            && foundItem.Latitude <= selectedRegion.LatitudeEnd
                            && foundItem.Longitude >= selectedRegion.LongitudeStart
                            && foundItem.Longitude <= selectedRegion.LongitudeEnd
                            )
                    );

                }

            }

            return foundItems;
        }



        public List<ASVFoundItem> GetItems(long tribeId, string className, string selectedRealm)
        {
            List<ASVFoundItem> foundItems = new List<ASVFoundItem>();

            Realm = selectedRealm;

            //get selected tribe(s)
            var tribes = GetTribes(tribeId);
            if (tribes != null && tribes.Count > 0)
            {
                foreach (var tribe in tribes)
                {

                    //list items in structures
                    if (tribe.Structures != null && tribe.Structures.Count > 0)
                    {
                        var matchedContainers = tribe.Structures.Where(s =>
                                s.Inventory.Items.Count > 0
                            )
                            .Select(s => new
                            {
                                tribe.TribeName,
                                Container = s.ClassName,
                                s.Latitude,
                                s.Longitude,
                                s.X,
                                s.Y,
                                s.Z,
                                MatchedItems = s.Inventory.Items.Where(i => i.ClassName == className || className == "").ToList()
                            })
                            .ToList();

                        if (matchedContainers != null && matchedContainers.Count > 0)
                        {
                            foreach (var container in matchedContainers)
                            {
                                var groupedItems = container.MatchedItems.GroupBy(x => new { ClassName = x.ClassName, IsBluePrint = x.IsBlueprint, x.Rating, x.UploadedTime }).Select(g => new { ClassName = g.Key.ClassName, IsBlueprint = g.Key.IsBluePrint, Rating = g.Key.Rating, UploadedTime =g.Key.UploadedTime, Qty = g.Sum(i => i.Quantity) }).ToList();

                                if (groupedItems != null && groupedItems.Count > 0)
                                {
                                    groupedItems.ForEach(g =>
                                    {
                                        string displayName = g.ClassName;
                                        var itemMap = Program.ProgramConfig.ItemMap.FirstOrDefault(m => m.ClassName == g.ClassName);
                                        if (itemMap != null) displayName = itemMap.DisplayName;

                                        string containerName = container.Container;
                                        var containerMap = Program.ProgramConfig.StructureMap.FirstOrDefault(m => m.ClassName == container.Container);
                                        if (containerMap != null)
                                        {
                                            containerName = containerMap.FriendlyName;
                                        }
                                        
                                        foundItems.Add(new ASVFoundItem()
                                        {
                                            ContainerName = containerName,
                                            TribeId = tribe.TribeId,
                                            TribeName = tribe.TribeName,
                                            ClassName = g.ClassName,
                                            DisplayName = displayName,
                                            Latitude = (decimal)container.Latitude.GetValueOrDefault(0),
                                            Longitude = (decimal)container.Longitude.GetValueOrDefault(0),
                                            X = (decimal)container.X,
                                            Y = (decimal)container.Y,
                                            Z = (decimal)container.Z,
                                            Quantity = g.Qty,
                                            IsBlueprint = g.IsBlueprint,
                                            Rating = g.Rating,
                                            UploadedTime = g.UploadedTime

                                        });

                                    });
                                }
                            }

                        }
                    }

                    //list items in structures
                    if (tribe.Tames != null && tribe.Tames.Count > 0)
                    {
                        var matchedContainers = tribe.Tames.Where(s =>
                                s.Inventory.Items.Count >0
                            )
                            .Select(s => new
                            {
                                tribe.TribeName,
                                Container = "Tame",
                                s.Latitude,
                                s.Longitude,
                                s.X,
                                s.Y,
                                s.Z,
                                MatchedItems = s.Inventory.Items.Where(i => i.ClassName == className || className == "").ToList()
                            }).ToList();

                        if (matchedContainers != null && matchedContainers.Count > 0)
                        {
                            foreach (var container in matchedContainers)
                            {
                                var groupedItems = container.MatchedItems.GroupBy(x => new { x.ClassName, x.IsBlueprint,  x.Rating } ).Select(g => new { ClassName = g.Key.ClassName, IsBlueprint = g.Key.IsBlueprint, Rating = g.Key.Rating, Qty = g.Sum(i => i.Quantity) }).ToList();

                                if (groupedItems != null && groupedItems.Count > 0)
                                {
                                    groupedItems.ForEach(g =>
                                    {
                                        string displayName = g.ClassName;
                                        var itemMap = Program.ProgramConfig.ItemMap.FirstOrDefault(m => m.ClassName == g.ClassName);
                                        if (itemMap != null) displayName = itemMap.DisplayName;


                                        foundItems.Add(new ASVFoundItem()
                                        {
                                            ContainerName = container.Container,
                                            TribeId = tribe.TribeId,
                                            TribeName = tribe.TribeName,
                                            ClassName = g.ClassName,
                                            DisplayName = displayName,
                                            Latitude = (decimal)container.Latitude.GetValueOrDefault(0),
                                            Longitude = (decimal)container.Longitude.GetValueOrDefault(0),
                                            X = (decimal)container.X,
                                            Y = (decimal)container.Y,
                                            Z = (decimal)container.Z,
                                            Quantity = g.Qty,
                                            IsBlueprint = g.IsBlueprint,
                                            Rating = g.Rating

                                        });

                                    });
                                }
                            }

                        }
                    }

                    //list items in players
                    if (tribe.Players != null && tribe.Players.Count > 0)
                    {
                        var matchedContainers = tribe.Players.Where(s =>
                                s.Inventory.Items.Count > 0
                            )
                            .Select(s => new
                            {
                                tribe.TribeName,
                                Container = "Player",
                                s.Latitude,
                                s.Longitude,
                                s.X,
                                s.Y,
                                s.Z,                                
                                MatchedItems = s.Inventory.Items.Where(i => i.ClassName == className || className == "").ToList()
                            })
                            .ToList();

                        if (matchedContainers != null && matchedContainers.Count > 0)
                        {
                            foreach (var container in matchedContainers)
                            {
                                var groupedItems = container.MatchedItems.GroupBy(x => new { x.ClassName, x.IsBlueprint, x.Rating, x.UploadedTime } ).Select(g => new { ClassName = g.Key.ClassName, IsBlueprint = g.Key.IsBlueprint, Rating = g.Key.Rating, UploadedTime = g.Key.UploadedTime, Qty = g.Sum(i => i.Quantity) }).ToList();

                                if (groupedItems != null && groupedItems.Count > 0)
                                {
                                    groupedItems.ForEach(g =>
                                    {
                                        string displayName = g.ClassName;
                                        var itemMap = Program.ProgramConfig.ItemMap.FirstOrDefault(m => m.ClassName == g.ClassName);
                                        if (itemMap != null) displayName = itemMap.DisplayName;

                                        foundItems.Add(new ASVFoundItem()
                                        {
                                            ContainerName = g.UploadedTime.HasValue?"Cluster":container.Container,
                                            TribeId = tribe.TribeId,
                                            TribeName = tribe.TribeName,
                                            ClassName = g.ClassName,
                                            DisplayName = displayName,
                                            Latitude = container.Latitude.HasValue?(decimal)container.Latitude.GetValueOrDefault(0):0,
                                            Longitude = container.Longitude.HasValue?(decimal)container.Longitude.GetValueOrDefault(0):0,
                                            X = container.X.HasValue?((decimal)container.X):0,
                                            Y = container.Y.HasValue ? ((decimal)container.Y) : 0,
                                            Z = container.Z.HasValue ? ((decimal)container.Z):0,
                                            Quantity = g.Qty,
                                            IsBlueprint = g.IsBlueprint,
                                            Rating = g.Rating,
                                            UploadedTime = g.UploadedTime
                                        });

                                    });
                                }
                            }

                        }


                    }
                }

            }

            if (!string.IsNullOrEmpty(selectedRealm))
            {
                if (LoadedMap.Regions != null && LoadedMap.Regions.Count > 0)
                {
                    var selectedRegion = LoadedMap.Regions.FirstOrDefault(r => r.RegionName == selectedRealm);
                    foundItems.RemoveAll(foundItem => !(
                            (float)foundItem.Z >= selectedRegion.ZStart
                            && (float)foundItem.Z <= selectedRegion.ZEnd
                            && (float)foundItem.Latitude >= selectedRegion.LatitudeStart
                            && (float)foundItem.Latitude <= selectedRegion.LatitudeEnd
                            && (float)foundItem.Longitude >= selectedRegion.LongitudeStart
                            && (float)foundItem.Longitude <= selectedRegion.LongitudeEnd
                            )
                    );

                }

            }

            return foundItems;
        }

        public List<ContentDroppedItem> GetDeathCacheBags(long playerId)
        {
            if (pack.DroppedItems == null) return new List<ContentDroppedItem>();
            return pack.DroppedItems
                .Where(d =>
                    d.IsDeathCache
                    && (d.DroppedByPlayerId == playerId || playerId == 0)
                ).ToList();
        }





        /**** Export options ****/
        public void ExportContentPack(string exportFilename)
        {
            pack.ExportPack(exportFilename);
        }

        public void ExportAll(string exportPath)
        {
            if (!Directory.Exists(exportPath)) Directory.CreateDirectory(exportPath);
            Task.WaitAll(
                Task.Run(() => ExportWild(Path.Combine(exportPath, "ASV_Wild.json"))),
                Task.Run(() => ExportPlayerTribes(Path.Combine(exportPath, "ASV_Tribes.json"))),
                Task.Run(() => ExportPlayerTribeLogs(Path.Combine(exportPath, "ASV_TribeLogs.json"))),
                Task.Run(() => ExportTamed(Path.Combine(exportPath, "ASV_Tamed.json"))),
                Task.Run(() => ExportPlayers(Path.Combine(exportPath, "ASV_Players.json"))),
                Task.Run(() => ExportPlayerStructures(Path.Combine(exportPath, "ASV_Structures.json"))),
                Task.Run(() => ExportMapStructures(Path.Combine(exportPath,"ASV_MapStructures.json")))
                );
            ;
        }

        public void ExportWild(string exportFilename)
        {

            pack.ExportJsonWild(exportFilename);
        }

        public void ExportTamed(string exportFilename)
        {
            pack.ExportJsonTamed(exportFilename);
        }

        public void ExportMapStructures(string exportFilename)
        {
            pack.ExportJsonMapStructures(exportFilename);

        }

        public void ExportPlayerStructures(string exportFilename)
        {
            pack.ExportJsonPlayerStructures(exportFilename);

            /*
            string exportFolder = Path.GetDirectoryName(exportFilename);
            if (!Directory.Exists(exportFolder)) Directory.CreateDirectory(exportFolder);


            using (FileStream fs = File.Create(exportFilename))
            {
                using (StreamWriter sw = new StreamWriter(fs))
                {
                    using (JsonTextWriter jw = new JsonTextWriter(sw))
                    {
                        jw.WriteStartArray();

                        foreach (var tribe in pack.Tribes)
                        {
                            var playerStructures = tribe.Structures;
                            foreach (var structure in playerStructures)
                            {
                                jw.WriteStartObject();

                                jw.WritePropertyName("tribeid");
                                jw.WriteValue(tribe.TribeId);


                                jw.WritePropertyName("tribe");
                                jw.WriteValue(tribe.TribeName);

                                jw.WritePropertyName("owner");
                                jw.WriteValue(structure.OwningPlayerName ?? tribe.TribeName);


                                jw.WritePropertyName("struct");
                                jw.WriteValue(structure.ClassName);

                                jw.WritePropertyName("lat");
                                jw.WriteValue(structure.Latitude.GetValueOrDefault(0));

                                jw.WritePropertyName("lon");
                                jw.WriteValue(structure.Longitude.GetValueOrDefault(0));

                                jw.WritePropertyName("ccc");
                                jw.WriteValue($"{structure.X} {structure.Y} {structure.Z}");

                                jw.WritePropertyName("created");
                                jw.WriteValue(structure.CreatedDateTime.Value.ToUniversalTime());
                                
                                jw.WritePropertyName("refreshed");
                                jw.WriteValue(structure.LastAllyInRangeTime.Value.ToUniversalTime());

                                if (Program.ProgramConfig.ExportInventories)
                                {
                                    jw.WritePropertyName("inventory");
                                    jw.WriteStartArray();
                                    if (structure.Inventory.Items.Count > 0)
                                    {
                                        foreach (var invItem in structure.Inventory.Items)
                                        {
                                            if (!invItem.IsEngram)
                                            {
                                                jw.WriteStartObject();

                                                jw.WritePropertyName("itemId");
                                                jw.WriteValue(invItem.ClassName);

                                                jw.WritePropertyName("qty");
                                                jw.WriteValue(invItem.Quantity);


                                                jw.WritePropertyName("blueprint");
                                                jw.WriteValue(invItem.IsBlueprint);

                                                var itemMap = Program.ProgramConfig.ItemMap.FirstOrDefault(m => m.ClassName == invItem.ClassName);
                                                if (itemMap != null)
                                                {
                                                    jw.WritePropertyName("category");
                                                    jw.WriteValue(itemMap.Category);
                                                }


                                                jw.WriteEndObject();
                                            }

                                        }
                                    }

                                    jw.WriteEndArray();
                                }

                                jw.WriteEnd();
                            }
                        }

                        jw.WriteEndArray();
                    }

                }

            }
            */
        }


        public void ExportPlayerTribeLogs(string exportFilename)
        {
            pack.ExportJsonPlayerTribeLogs(exportFilename);
            /*

            string exportFolder = Path.GetDirectoryName(exportFilename);
            if (!Directory.Exists(exportFolder)) Directory.CreateDirectory(exportFolder);


            using (FileStream fs = File.Create(exportFilename))
            {
                using (StreamWriter sw = new StreamWriter(fs))
                {
                    using (JsonTextWriter jw = new JsonTextWriter(sw))
                    {
                        jw.WriteStartArray();

                        foreach (var playerTribe in pack.Tribes)
                        {
                            jw.WriteStartObject();

                            jw.WritePropertyName("tribeid");
                            jw.WriteValue(playerTribe.TribeId);

                            jw.WritePropertyName("tribe");
                            jw.WriteValue(playerTribe.TribeName);


                            if (playerTribe.Logs != null && playerTribe.Logs.Length > 0)
                            {
                                jw.WritePropertyName("logs");
                                jw.WriteStartArray();
                                foreach (var logEntry in playerTribe.Logs)
                                {

                                    jw.WriteValue(logEntry);
                                }

                                jw.WriteEndArray();
                            }

                            jw.WriteEnd();
                        }

                        jw.WriteEndArray();
                    }

                }

            }
            */

        }


        public void ExportPlayerTribes(string exportFilename)
        {
            pack.ExportJsonPlayerTribes(exportFilename);


            /*
            if (File.Exists(exportFilename)) File.Delete(exportFilename);

            using (StreamWriter sw = new StreamWriter(exportFilename))
            {
                using (JsonTextWriter jw = new JsonTextWriter(sw))
                {
                    jw.WriteStartArray();

                    foreach (var playerTribe in pack.Tribes)
                    {
                        jw.WriteStartObject();

                        jw.WritePropertyName("tribeid");
                        jw.WriteValue(playerTribe.TribeId);

                        jw.WritePropertyName("tribe");
                        jw.WriteValue(playerTribe.TribeName);

                        jw.WritePropertyName("players");
                        jw.WriteValue(playerTribe.Players.Count);

                        if (playerTribe.Players != null && playerTribe.Players.Count > 0)
                        {
                            jw.WritePropertyName("members");
                            jw.WriteStartArray();
                            foreach (var tribePlayer in playerTribe.Players)
                            {
                                jw.WriteStartObject();

                                jw.WritePropertyName("ign");
                                jw.WriteValue(tribePlayer.CharacterName);

                                jw.WritePropertyName("lvl");
                                jw.WriteValue(tribePlayer.Level.ToString());

                                jw.WritePropertyName("playerid");
                                jw.WriteValue(tribePlayer.Id.ToString());

                                jw.WritePropertyName("playername");
                                jw.WriteValue(tribePlayer.Name);

                                jw.WritePropertyName("steamid");
                                jw.WriteValue(tribePlayer.NetworkId);

                                jw.WriteEndObject();
                            }

                            jw.WriteEndArray();
                        }



                        jw.WritePropertyName("tames");
                        jw.WriteValue(playerTribe.Tames.Count);

                        jw.WritePropertyName("structures");
                        jw.WriteValue(playerTribe.Structures.Count);

                        jw.WritePropertyName("active");
                        jw.WriteValue(playerTribe.LastActive);


                        jw.WriteEnd();
                    }

                    jw.WriteEndArray();
                }

            }
            */

        }

        public void ExportPlayers(string exportFilename)
        {
            pack.ExportJsonPlayers(exportFilename);

            /*
            string exportFolder = Path.GetDirectoryName(exportFilename);
            if (!Directory.Exists(exportFolder)) Directory.CreateDirectory(exportFolder);

            using (FileStream fs = File.Create(exportFilename))
            {
                using (StreamWriter sw = new StreamWriter(fs))
                {
                    using (JsonTextWriter jw = new JsonTextWriter(sw))
                    {
                        jw.WriteStartArray();

                        foreach (var tribe in pack.Tribes)
                        {
                            foreach (var player in tribe.Players)
                            {
                                jw.WriteStartObject();

                                jw.WritePropertyName("playerid");
                                jw.WriteValue(player.Id);

                                jw.WritePropertyName("steam");
                                jw.WriteValue(player.Name);

                                jw.WritePropertyName("name");
                                jw.WriteValue(player.CharacterName);

                                jw.WritePropertyName("tribeid");
                                jw.WriteValue(tribe.TribeId);

                                jw.WritePropertyName("tribe");
                                jw.WriteValue(tribe.TribeName);

                                jw.WritePropertyName("sex");
                                jw.WriteValue(player.Gender);

                                jw.WritePropertyName("lvl");
                                jw.WriteValue(player.Level);

                                jw.WritePropertyName("lat");
                                jw.WriteValue(player.Latitude.GetValueOrDefault(0));

                                jw.WritePropertyName("lon");
                                jw.WriteValue(player.Longitude.GetValueOrDefault(0));



                                //0=health
                                //1=stamina
                                //2=torpor
                                //3=oxygen
                                //4=food
                                //5=water
                                //6=temperature
                                //7=weight
                                //8=melee damage
                                //9=movement speed
                                //10=fortitude
                                //11=crafting speed
                                jw.WritePropertyName("hp");
                                jw.WriteValue(player.Stats[0]);

                                jw.WritePropertyName("stam");
                                jw.WriteValue(player.Stats[1]);

                                jw.WritePropertyName("melee");
                                jw.WriteValue(player.Stats[8]);

                                jw.WritePropertyName("weight");
                                jw.WriteValue(player.Stats[7]);

                                jw.WritePropertyName("speed");
                                jw.WriteValue(player.Stats[9]);

                                jw.WritePropertyName("food");
                                jw.WriteValue(player.Stats[4]);

                                jw.WritePropertyName("water");
                                jw.WriteValue(player.Stats[5]);

                                jw.WritePropertyName("oxy");
                                jw.WriteValue(player.Stats[3]);

                                jw.WritePropertyName("craft");
                                jw.WriteValue(player.Stats[11]);

                                jw.WritePropertyName("fort");
                                jw.WriteValue(player.Stats[10]);

                                jw.WritePropertyName("active");
                                jw.WriteValue(player.LastActiveDateTime);

                                jw.WritePropertyName("ccc");
                                jw.WriteValue($"{player.X} {player.Y} {player.Z}");

                                jw.WritePropertyName("steamid");
                                jw.WriteValue(player.NetworkId);

                                if (Program.ProgramConfig.ExportInventories)
                                {
                                    jw.WritePropertyName("inventory");
                                    jw.WriteStartArray();

                                    if (player.Inventory.Items.Count > 0)
                                    {
                                        foreach (var invItem in player.Inventory.Items)
                                        {
                                            if (!invItem.IsEngram && invItem.ClassName != "PrimalItem_StartingNote_C")
                                            {
                                                jw.WriteStartObject();

                                                jw.WritePropertyName("itemId");
                                                jw.WriteValue(invItem.ClassName);

                                                jw.WritePropertyName("qty");
                                                jw.WriteValue(invItem.Quantity);

                                                jw.WritePropertyName("blueprint");
                                                jw.WriteValue(invItem.IsBlueprint);

                                                var itemMap = Program.ProgramConfig.ItemMap.FirstOrDefault(m => m.ClassName == invItem.ClassName);
                                                if (itemMap != null)
                                                {
                                                    jw.WritePropertyName("category");
                                                    jw.WriteValue(itemMap.Category);
                                                }

                                                if (invItem.UploadedTimeInGame != 0)
                                                {
                                                    jw.WritePropertyName("uploadedTime");
                                                    jw.WriteValue(invItem.UploadedTime);
                                                }

                                                jw.WriteEndObject();
                                            }

                                        }
                                    }



                                    jw.WriteEndArray();
                                }


                                jw.WriteEnd();
                            }
                        }

                        jw.WriteEndArray();
                    }

                }

            }
            */
        }



        public Bitmap GetMapImageItems(long tribeId, string className, decimal selectedLat, decimal selectedLon, ASVStructureOptions mapOptions, List<ContentMarker> customMarkers, string selectedRealm)
        {
            Bitmap bitmap = new Bitmap(1024, 1024);
            Graphics graphics = Graphics.FromImage(bitmap);
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;


            if (
                cachedOptions.Equals(mapOptions)
                &&
                (
                    cacheImageItems != null
                    && cacheImageItems.Item1 == tribeId
                    && cacheImageItems.Item2 == className
                    && cacheImageItems.Item3 == selectedLat
                    && cacheImageItems.Item4 == selectedLon
                    && lastDrawRequest == "items"
                )
            )
            {
                //if all match, return cached content image
                graphics.DrawImage(gameContentMap, 0, 0);
            }
            else
            {
                lastDrawRequest = "items";
                cacheImageItems = new Tuple<long, string, decimal, decimal>(tribeId, className, selectedLon, selectedLon);
                cachedOptions = mapOptions;

                graphics.DrawImage(MapImage, new Rectangle(0, 0, 1024, 1024));
                graphics = AddMapStructures(graphics, mapOptions);


                var filteredResults = GetItems(tribeId, className, selectedRealm);
                foreach (var result in filteredResults)
                {
                    var markerX = (decimal)(result.Longitude) * 1024 / 100;
                    var markerY = (decimal)(result.Latitude) * 1024 / 100;
                    var markerSize = 10f;

                    Color markerColor = Color.AliceBlue;

                    graphics.FillEllipse(new SolidBrush(markerColor), (float)markerX - (markerSize / 2), (float)markerY - (markerSize / 2), markerSize, markerSize);

                    Color borderColour = Color.Blue;
                    int borderSize = 1;
                    graphics.DrawEllipse(new Pen(borderColour, borderSize), (float)markerX - (markerSize / 2), (float)markerY - (markerSize / 2), markerSize, markerSize);
                }

                gameContentMap = (Image)bitmap;
            }



            if (customMarkers != null && customMarkers.Count > 0)
            {
                graphics = AddCustomMarkers(graphics, customMarkers);
            }

            graphics = AddCurrentMarker(graphics, selectedLat, selectedLon);

            return bitmap;
        }

        /**** Map & Overlays ****/
        public Bitmap GetMapImageWild(string className, string productionClassName, int minLevel, int maxLevel, float filterLat, float filterLon, float filterRadius, decimal? selectedLat, decimal? selectedLon, ASVStructureOptions mapOptions, List<ContentMarker> customMarkers, string selectedRealm)
        {
            Bitmap bitmap = new Bitmap(1024, 1024);
            Graphics graphics = Graphics.FromImage(bitmap);
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;

            if (
                cachedOptions.Equals(mapOptions)
                && (cacheImageWild != null
                    && cacheImageWild.Item1 == className
                    && cacheImageWild.Item2 == productionClassName
                    && cacheImageWild.Item3 == minLevel
                    && cacheImageWild.Item4 == maxLevel
                    && cacheImageWild.Item5 == filterLat
                    && cacheImageWild.Item6 == filterLon
                    && cacheImageWild.Item7 == filterRadius
                    && lastRealm == selectedRealm
                    && lastDrawRequest == "wild"
                    )
            )
            {
                //if all match, return cached content image
                graphics.DrawImage(gameContentMap, 0, 0);
            }
            else
            {
                lastDrawRequest = "wild";
                cacheImageWild = new Tuple<string, string, int, int, float, float, float>(className, productionClassName, minLevel, maxLevel, filterLat, filterLon, filterRadius);
                lastRealm = selectedRealm;
                cachedOptions = mapOptions;

                graphics.DrawImage(MapImage, new Rectangle(0, 0, 1024, 1024));
                graphics = AddMapStructures(graphics, mapOptions);

                var filteredWilds = GetWildCreatures(minLevel, maxLevel, filterLat, filterLon, filterRadius, className, selectedRealm);
                
                //remove any not matching productionClass
                if(productionClassName.Length > 0) filteredWilds.RemoveAll(d => d.ProductionResources == null || !d.ProductionResources.Any(r => r == productionClassName));


                foreach (var wild in filteredWilds)
                {
                    var markerX = (decimal)(wild.Longitude.GetValueOrDefault(0)) * 1024 / 100;
                    var markerY = (decimal)(wild.Latitude.GetValueOrDefault(0)) * 1024 / 100;
                    var markerSize = 10f;


                    Color markerColor = Color.WhiteSmoke;
                    if (LoadedMap.Regions != null && LoadedMap.Regions.Count > 0)
                    {
                        var insideRegion = LoadedMap.Regions.FirstOrDefault(r =>
                            wild.Z >= r.ZStart
                            && wild.Z <= r.ZEnd
                            && wild.Latitude >= r.LatitudeStart
                            && wild.Latitude <= r.LatitudeEnd
                            && wild.Longitude >= r.LongitudeStart
                            && wild.Longitude <= r.LongitudeEnd
                        );

                        if (insideRegion != null)
                        {
                            markerColor = ColorTranslator.FromHtml(insideRegion.MarkerColor);
                        }

                    }

                    graphics.FillEllipse(new SolidBrush(markerColor), (float)markerX - (markerSize / 2), (float)markerY - (markerSize / 2), markerSize, markerSize);

                    Color borderColour = Color.Blue;
                    int borderSize = 1;
                    graphics.DrawEllipse(new Pen(borderColour, borderSize), (float)markerX - (markerSize / 2), (float)markerY - (markerSize / 2), markerSize, markerSize);
                }

                gameContentMap = (Image)bitmap;
            }


            if (customMarkers != null && customMarkers.Count > 0)
            {
                graphics = AddCustomMarkers(graphics, customMarkers);
            }

            graphics = AddCurrentMarker(graphics, selectedLat, selectedLon);
            
            

            return bitmap;
        }

        public Bitmap GetMapImageTamed(string className, string productionClassName, bool includeStored, long tribeId, long playerId, decimal? selectedLat, decimal? selectedLon, ASVStructureOptions mapOptions, List<ContentMarker> customMarkers, string selectedRealm)
        {
            Bitmap bitmap = new Bitmap(1024, 1024);
            Graphics graphics = Graphics.FromImage(bitmap);
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;


            if (cachedOptions.Equals(mapOptions) 
                && (cacheImageTamed != null
                && cacheImageTamed.Item1 == className
                && cacheImageTamed.Item2 == productionClassName
                && cacheImageTamed.Item3 == includeStored
                && cacheImageTamed.Item4 == tribeId
                && cacheImageTamed.Item5 == playerId
                && lastRealm == selectedRealm
                && lastDrawRequest == "tamed")
            )
            {
                //if all match, return cached content image
                graphics.DrawImage(gameContentMap, 0, 0);
            }
            else
            {
                lastDrawRequest = "tamed";
                lastRealm = selectedRealm;
                cacheImageTamed = new Tuple<string, string, bool, long, long>(className, productionClassName, includeStored, tribeId, playerId);
                cachedOptions = mapOptions;

                graphics.DrawImage(MapImage, new Rectangle(0, 0, 1024, 1024));
                graphics = AddMapStructures(graphics, mapOptions);


                var filteredTames = GetTamedCreatures(className, tribeId, playerId, includeStored, selectedRealm);
                //remove any not matching productionClass
                if (productionClassName.Length > 0) filteredTames.RemoveAll(d => d.ProductionResources == null || !d.ProductionResources.Any(r => r == productionClassName));

                foreach (var tame in filteredTames)
                {
                    var markerX = (decimal)(tame.Longitude.GetValueOrDefault(0)) * 1024 / 100;
                    var markerY = (decimal)(tame.Latitude.GetValueOrDefault(0)) * 1024 / 100;
                    var markerSize = 10f;

                    Color markerColor = Color.WhiteSmoke;
                    if (LoadedMap.Regions != null && LoadedMap.Regions.Count > 0)
                    {
                        var insideRegion = LoadedMap.Regions.FirstOrDefault(r =>
                            tame.Z >= r.ZStart
                            && tame.Z <= r.ZEnd
                            && tame.Latitude >= r.LatitudeStart
                            && tame.Latitude <= r.LatitudeEnd
                            && tame.Longitude >= r.LongitudeStart
                            && tame.Longitude <= r.LongitudeEnd
                        );

                        if (insideRegion != null)
                        {
                            markerColor = ColorTranslator.FromHtml(insideRegion.MarkerColor);
                        }

                    }

                    graphics.FillEllipse(new SolidBrush(markerColor), (float)markerX - (markerSize / 2), (float)markerY - (markerSize / 2), markerSize, markerSize);

                    Color borderColour = Color.Blue;
                    int borderSize = 1;
                    graphics.DrawEllipse(new Pen(borderColour, borderSize), (float)markerX - (markerSize / 2), (float)markerY - (markerSize / 2), markerSize, markerSize);
                }

                gameContentMap = (Image)bitmap;

            }

            if (customMarkers != null && customMarkers.Count > 0)
            {
                graphics = AddCustomMarkers(graphics, customMarkers);
            }

            graphics = AddCurrentMarker(graphics, selectedLat, selectedLon);

            return bitmap;
        }

        public Bitmap GetMapImageDroppedItems(long droppedPlayerId, string droppedClass, decimal? selectedLat, decimal? selectedLon, ASVStructureOptions mapOptions, List<ContentMarker> customMarkers,string selectedRealm)
        {

            Bitmap bitmap = new Bitmap(1024, 1024);
            Graphics graphics = Graphics.FromImage(bitmap);
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;


            if (cachedOptions.Equals(mapOptions)
                &&(cacheImageDroppedItems != null
                && cacheImageDroppedItems.Item1 == droppedPlayerId
                && cacheImageDroppedItems.Item2 == droppedClass
                && lastRealm == selectedRealm
                && lastDrawRequest == "droppeditems")
            
            )
            {
                //if all match, return cached content image
                graphics.DrawImage(gameContentMap, 0, 0);
            }
            else
            {
                lastDrawRequest = "droppeditems";
                lastRealm = selectedRealm;
                cacheImageDroppedItems = new Tuple<long, string>(droppedPlayerId, droppedClass);
                cachedOptions = mapOptions;

                graphics.DrawImage(MapImage, new Rectangle(0, 0, 1024, 1024));
                graphics = AddMapStructures(graphics, mapOptions);

                var filteredDrops = GetDroppedItems(droppedPlayerId, droppedClass, selectedRealm);
                float markerSize = 10f;
                foreach (var item in filteredDrops)
                {

                    float latitude = item.Latitude.GetValueOrDefault(0);
                    float longitude = item.Longitude.GetValueOrDefault(0);

                    var markerX = (decimal)(longitude) * 1024 / 100;
                    var markerY = (decimal)(latitude) * 1024 / 100;

                    Color markerColor = Color.WhiteSmoke;
                    if (LoadedMap.Regions != null && LoadedMap.Regions.Count > 0)
                    {
                        var insideRegion = LoadedMap.Regions.FirstOrDefault(r =>
                            item.Z >= r.ZStart
                            && item.Z <= r.ZEnd
                            && item.Latitude >= r.LatitudeStart
                            && item.Latitude <= r.LatitudeEnd
                            && item.Longitude >= r.LongitudeStart
                            && item.Longitude <= r.LongitudeEnd
                        );

                        if (insideRegion != null)
                        {
                            markerColor = ColorTranslator.FromHtml(insideRegion.MarkerColor);
                        }

                    }
                    graphics.FillEllipse(new SolidBrush(markerColor), (float)markerX - (markerSize / 2), (float)markerY - (markerSize / 2), markerSize, markerSize);

                    Color borderColour = Color.Blue;
                    int borderSize = 1;
                    graphics.DrawEllipse(new Pen(borderColour, borderSize), (float)markerX - (markerSize / 2), (float)markerY - (markerSize / 2), markerSize, markerSize);
                }

                gameContentMap = (Image)bitmap;
            }

            if (customMarkers != null && customMarkers.Count > 0)
            {
                graphics = AddCustomMarkers(graphics, customMarkers);
            }

            graphics = AddCurrentMarker(graphics, selectedLat, selectedLon);

            return bitmap;



        }

        public Bitmap GetMapImageDropBags(long droppedPlayerId, decimal? selectedLat, decimal? selectedLon, ASVStructureOptions mapOptions, List<ContentMarker> customMarkers)
        {
            Bitmap bitmap = new Bitmap(1024, 1024);
            Graphics graphics = Graphics.FromImage(bitmap);
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;


            if (cachedOptions.Equals(mapOptions)
                && (cacheImageDropBags != null
                && droppedPlayerId == cacheImageDropBags.Item1
                && lastDrawRequest == "dropbags")
            )
            {
                //if all match, return cached content image
                graphics.DrawImage(gameContentMap, 0, 0);
            }
            else
            {
                lastDrawRequest = "dropbags";
                cacheImageDropBags = new Tuple<long>(droppedPlayerId);
                cachedOptions = mapOptions;

                graphics.DrawImage(MapImage, new Rectangle(0, 0, 1024, 1024));
                graphics = AddMapStructures(graphics, mapOptions);


                var filteredDrops = GetDeathCacheBags(droppedPlayerId);
                float markerSize = 10f;
                foreach (var item in filteredDrops)
                {

                    float latitude = item.Latitude.GetValueOrDefault(0);
                    float longitude = item.Longitude.GetValueOrDefault(0);

                    var markerX = (decimal)(longitude) * 1024 / 100;
                    var markerY = (decimal)(latitude) * 1024 / 100;

                    Color markerColor = Color.WhiteSmoke;
                    if (LoadedMap.Regions != null && LoadedMap.Regions.Count > 0)
                    {
                        var insideRegion = LoadedMap.Regions.FirstOrDefault(r =>
                            item.Z >= r.ZStart
                            && item.Z <= r.ZEnd
                            && item.Latitude >= r.LatitudeStart
                            && item.Latitude <= r.LatitudeEnd
                            && item.Longitude >= r.LongitudeStart
                            && item.Longitude <= r.LongitudeEnd
                        );

                        if (insideRegion != null)
                        {
                            markerColor = ColorTranslator.FromHtml(insideRegion.MarkerColor);
                        }

                    }
                    graphics.FillEllipse(new SolidBrush(markerColor), (float)markerX - (markerSize / 2), (float)markerY - (markerSize / 2), markerSize, markerSize);

                    Color borderColour = Color.Blue;
                    int borderSize = 1;
                    graphics.DrawEllipse(new Pen(borderColour, borderSize), (float)markerX - (markerSize / 2), (float)markerY - (markerSize / 2), markerSize, markerSize);
                }

                gameContentMap = (Image)bitmap;
            }

            if (customMarkers != null && customMarkers.Count > 0)
            {
                graphics = AddCustomMarkers(graphics, customMarkers);
            }

            graphics = AddCurrentMarker(graphics, selectedLat, selectedLon);

            return bitmap;
        }

        public Bitmap GetMapImagePlayerStructures(string className, long tribeId, long playerId, decimal? selectedLat, decimal? selectedLon, ASVStructureOptions mapOptions, List<ContentMarker> customMarkers, string selectedRealm)
        {
            Bitmap bitmap = new Bitmap(1024, 1024);
            Graphics graphics = Graphics.FromImage(bitmap);
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;


            if (cachedOptions.Equals(mapOptions)
                && (cacheImagePlayerStructures != null
                && cacheImagePlayerStructures.Item1 == className
                && cacheImagePlayerStructures.Item2 == tribeId
                && cacheImagePlayerStructures.Item3 == playerId
                && lastRealm == selectedRealm
                && lastDrawRequest == "structures")
            )
            {
                //if all match, return cached content image
                graphics.DrawImage(gameContentMap, 0, 0);
            }
            else
            {
                lastDrawRequest = "structures";
                lastRealm = selectedRealm;
                cacheImagePlayerStructures = new Tuple<string, long, long>(className, tribeId, playerId);
                cachedOptions = mapOptions;

                graphics.DrawImage(MapImage, new Rectangle(0, 0, 1024, 1024));
                graphics = AddMapStructures(graphics, mapOptions);

                var filteredStructures = GetPlayerStructures(tribeId, playerId, className, false, selectedRealm);
                foreach (var playerStructure in filteredStructures)
                {
                    var markerX = (decimal)(playerStructure.Longitude.GetValueOrDefault(0)) * 1024 / 100;
                    var markerY = (decimal)(playerStructure.Latitude.GetValueOrDefault(0)) * 1024 / 100;
                    var markerSize = 10f;

                    Color markerColor = Color.WhiteSmoke;
                    if (LoadedMap.Regions != null && LoadedMap.Regions.Count > 0)
                    {
                        var insideRegion = LoadedMap.Regions.FirstOrDefault(r =>
                            playerStructure.Z >= r.ZStart
                            && playerStructure.Z <= r.ZEnd
                            && playerStructure.Latitude >= r.LatitudeStart
                            && playerStructure.Latitude <= r.LatitudeEnd
                            && playerStructure.Longitude >= r.LongitudeStart
                            && playerStructure.Longitude <= r.LongitudeEnd
                        );

                        if (insideRegion != null)
                        {
                            markerColor = ColorTranslator.FromHtml(insideRegion.MarkerColor);
                        }

                    }
                    graphics.FillEllipse(new SolidBrush(markerColor), (float)markerX - (markerSize / 2), (float)markerY - (markerSize / 2), markerSize, markerSize);

                    Color borderColour = Color.Blue;
                    int borderSize = 1;
                    graphics.DrawEllipse(new Pen(borderColour, borderSize), (float)markerX - (markerSize / 2), (float)markerY - (markerSize / 2), markerSize, markerSize);

                }

                gameContentMap = (Image)bitmap;

            }


            if (customMarkers != null && customMarkers.Count > 0)
            {
                graphics = AddCustomMarkers(graphics, customMarkers);
            }

            graphics = AddCurrentMarker(graphics, selectedLat, selectedLon);

            return bitmap;
        }

        public Bitmap GetMapImageTribes(long tribeId, bool showStructures, bool showPlayers, bool showTames, decimal? selectedLat, decimal? selectedLon, ASVStructureOptions mapOptions, List<ContentMarker> customMarkers)
        {
            Bitmap bitmap = new Bitmap(1024, 1024);
            Graphics graphics = Graphics.FromImage(bitmap);
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;


            if (cacheImageTribes != null
                && cacheImageTribes.Item1 == tribeId
                && cacheImageTribes.Item2 == showStructures
                && cacheImageTribes.Item3 == showPlayers
                && cacheImageTribes.Item4 == showTames
                && lastDrawRequest == "tribes")
            {
                //if all match, return cached content image
                graphics.DrawImage(gameContentMap, 0, 0);
            }
            else
            {
                lastDrawRequest = "tribes";
                cacheImageTribes = new Tuple<long, bool, bool, bool>(tribeId, showStructures, showPlayers, showTames);

                graphics.DrawImage(MapImage, new Rectangle(0, 0, 1024, 1024));
                graphics = AddMapStructures(graphics, mapOptions);


                var tribe = GetTribes(tribeId).FirstOrDefault<ContentTribe>();
                if (tribe != null)
                {
                    if (showStructures)
                    {
                        var tribeStructures = tribe.Structures.Where(s => !Program.ProgramConfig.StructureExclusions.Any(e => e.ToLower() == s.ClassName.ToLower())).ToList();
                        if (tribeStructures.Count() > 0)
                        {
                            foreach (var structure in tribeStructures)
                            {
                                if (!(structure.Latitude.GetValueOrDefault(0) == 0 && structure.Longitude.GetValueOrDefault(0) == 0))
                                {
                                    float markerSize = 10f;

                                    var markerX = (decimal)(structure.Longitude.GetValueOrDefault(0)) * 1024 / 100;
                                    var markerY = (decimal)(structure.Latitude.GetValueOrDefault(0)) * 1024 / 100;

                                    Color markerColor = Color.WhiteSmoke;
                                    if (LoadedMap.Regions != null && LoadedMap.Regions.Count > 0)
                                    {
                                        var insideRegion = LoadedMap.Regions.FirstOrDefault(r =>
                                            structure.Z >= r.ZStart
                                            && structure.Z <= r.ZEnd
                                            && structure.Latitude >= r.LatitudeStart
                                            && structure.Latitude <= r.LatitudeEnd
                                            && structure.Longitude >= r.LongitudeStart
                                            && structure.Longitude <= r.LongitudeEnd
                                        );

                                        if (insideRegion != null)
                                        {
                                            markerColor = ColorTranslator.FromHtml(insideRegion.MarkerColor);
                                        }

                                    }
                                    graphics.FillEllipse(new SolidBrush(markerColor), (float)markerX - (markerSize / 2), (float)markerY - (markerSize / 2), markerSize, markerSize);

                                    Color borderColour = Color.Green;
                                    int borderSize = 1;
                                    graphics.DrawEllipse(new Pen(borderColour, borderSize), (float)markerX - (markerSize / 2), (float)markerY - (markerSize / 2), markerSize, markerSize);
                                }


                            }
                        }


                    }

                    if (showTames)
                    {
                        var tribeTames = tribe.Tames; ;
                        if (tribeTames.Count() > 0)
                        {
                            foreach (var tame in tribeTames)
                            {
                                if (!(tame.Latitude.GetValueOrDefault(0) == 0 && tame.Longitude.GetValueOrDefault(0) == 0))
                                {
                                    float markerSize = 10f;

                                    var markerX = (decimal)(tame.Longitude.GetValueOrDefault(0)) * 1024 / 100;
                                    var markerY = (decimal)(tame.Latitude.GetValueOrDefault(0)) * 1024 / 100;

                                    Color markerColor = Color.WhiteSmoke;
                                    if (LoadedMap.Regions != null && LoadedMap.Regions.Count > 0)
                                    {
                                        var insideRegion = LoadedMap.Regions.FirstOrDefault(r =>
                                            tame.Z >= r.ZStart
                                            && tame.Z <= r.ZEnd
                                            && tame.Latitude >= r.LatitudeStart
                                            && tame.Latitude <= r.LatitudeEnd
                                            && tame.Longitude >= r.LongitudeStart
                                            && tame.Longitude <= r.LongitudeEnd
                                        );

                                        if (insideRegion != null)
                                        {
                                            markerColor = ColorTranslator.FromHtml(insideRegion.MarkerColor);
                                        }

                                    }

                                    graphics.FillEllipse(new SolidBrush(Color.AliceBlue), (float)markerX - (markerSize / 2), (float)markerY - (markerSize / 2), markerSize, markerSize);

                                    Color borderColour = Color.Gold;
                                    int borderSize = 1;
                                    graphics.DrawEllipse(new Pen(borderColour, borderSize), (float)markerX - (markerSize / 2), (float)markerY - (markerSize / 2), markerSize, markerSize);
                                }


                            }
                        }



                    }

                    if (showPlayers)
                    {
                        var tribePlayers = tribe.Players;

                        if (tribePlayers != null && tribePlayers.Count() > 0)
                        {
                            foreach (var player in tribePlayers)
                            {
                                if (!(player.Latitude.GetValueOrDefault(0) == 0 && player.Longitude.GetValueOrDefault(0) == 0))
                                {

                                    var markerX = (decimal)(player.Longitude.GetValueOrDefault(0)) * 1024 / 100;
                                    var markerY = (decimal)(player.Latitude.GetValueOrDefault(0)) * 1024 / 100;

                                    Color markerColor = Color.WhiteSmoke;
                                    if (LoadedMap.Regions != null && LoadedMap.Regions.Count > 0)
                                    {
                                        var insideRegion = LoadedMap.Regions.FirstOrDefault(r =>
                                            player.Z >= r.ZStart
                                            && player.Z <= r.ZEnd
                                            && player.Latitude >= r.LatitudeStart
                                            && player.Latitude <= r.LatitudeEnd
                                            && player.Longitude >= r.LongitudeStart
                                            && player.Longitude <= r.LongitudeEnd
                                        );

                                        if (insideRegion != null)
                                        {
                                            markerColor = ColorTranslator.FromHtml(insideRegion.MarkerColor);
                                        }

                                    }

                                    graphics.FillEllipse(new SolidBrush(markerColor), (float)markerX - 15.0f, (float)markerY - 15.0f, 30, 30);

                                    Color borderColour = Color.Blue;
                                    int borderSize = 1;
                                    graphics.DrawEllipse(new Pen(borderColour, borderSize), (float)markerX - 15.0f, (float)markerY - 15.0f, 30, 30);

                                    Bitmap playerMarker = new Bitmap(ARKViewer.Properties.Resources.marker_28, new Size(20, 20));
                                    if (player.Gender.ToLower() == "female")
                                    {
                                        playerMarker = new Bitmap(ARKViewer.Properties.Resources.marker_29, new Size(20, 20));
                                    }
                                    graphics.DrawImage(playerMarker, (float)markerX - 10.0f, (float)markerY - 10.0f);
                                }
                            }
                        }

                    }

                }

                gameContentMap = (Image)bitmap;
            }

            if (customMarkers != null && customMarkers.Count > 0)
            {
                graphics = AddCustomMarkers(graphics, customMarkers);
            }

            graphics = AddCurrentMarker(graphics, selectedLat, selectedLon);

            return bitmap;
        }

        public Bitmap GetMapImagePlayers(long tribeId, long playerId, decimal? selectedLat, decimal? selectedLon, ASVStructureOptions mapOptions, List<ContentMarker> customMarkers, string selectedRealm)
        {
            Bitmap bitmap = new Bitmap(1024, 1024);
            Graphics graphics = Graphics.FromImage(bitmap);
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;


            if (cachedOptions.Equals(mapOptions)
                && (cacheImagePlayers != null
                && cacheImagePlayers.Item1 == tribeId
                && cacheImagePlayers.Item2 == playerId
                && lastRealm == selectedRealm
                && lastDrawRequest == "players")
            )
            {
                //if all match, return cached content image
                graphics.DrawImage(gameContentMap, 0, 0);
            }
            else
            {
                lastDrawRequest = "players";
                lastRealm = selectedRealm;

                cacheImagePlayers = new Tuple<long, long>(tribeId, playerId);
                cachedOptions = mapOptions;

                graphics.DrawImage(MapImage, new Rectangle(0, 0, 1024, 1024));
                graphics = AddMapStructures(graphics, mapOptions);

                var filteredPlayers = GetPlayers(tribeId, playerId, selectedRealm);
                foreach (var player in filteredPlayers)
                {
                    if (!(player.Latitude.GetValueOrDefault(0) == 0 && player.Longitude.GetValueOrDefault(0) == 0))
                    {
                        var markerX = (decimal)(player.Longitude.GetValueOrDefault(0)) * 1024 / 100;
                        var markerY = (decimal)(player.Latitude.GetValueOrDefault(0)) * 1024 / 100;

                        Color markerColor = Color.WhiteSmoke;
                        if (LoadedMap.Regions != null && LoadedMap.Regions.Count > 0)
                        {
                            var insideRegion = LoadedMap.Regions.FirstOrDefault(r =>
                                player.Z >= r.ZStart
                                && player.Z <= r.ZEnd
                                && player.Latitude >= r.LatitudeStart
                                && player.Latitude <= r.LatitudeEnd
                                && player.Longitude >= r.LongitudeStart
                                && player.Longitude <= r.LongitudeEnd
                            );

                            if (insideRegion != null)
                            {
                                markerColor = ColorTranslator.FromHtml(insideRegion.MarkerColor);
                            }

                        }

                        graphics.FillEllipse(new SolidBrush(markerColor), (float)markerX - 15.0f, (float)markerY - 15.0f, 30, 30);

                        Color borderColour = Color.Blue;
                        int borderSize = 1;

                        graphics.DrawEllipse(new Pen(borderColour, borderSize), (float)markerX - 15.0f, (float)markerY - 15.0f, 30, 30);

                        Bitmap playerMarker = new Bitmap(ARKViewer.Properties.Resources.marker_28, new Size(20, 20));
                        if (player.Gender == "Female")
                        {
                            playerMarker = new Bitmap(ARKViewer.Properties.Resources.marker_29, new Size(20, 20));
                        }
                        graphics.DrawImage(playerMarker, (float)markerX - 10.0f, (float)markerY - 10.0f);
                    }

                }


                gameContentMap = (Image)bitmap;

            }



            
            if (customMarkers != null && customMarkers.Count > 0)
            {
                graphics = AddCustomMarkers(graphics, customMarkers);
            }

            graphics = AddCurrentMarker(graphics, selectedLat, selectedLon);

            return bitmap;
        }

        public Bitmap GetMapImageMapStructures(List<ContentMarker> customMarkers, decimal? selectedLat, decimal? selectedLon)
        {
            Bitmap bitmap = new Bitmap(1024, 1024);
            Graphics graphics = Graphics.FromImage(bitmap);
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;


            graphics.DrawImage(MapImage, new Rectangle(0, 0, 1024, 1024));


            var config = Program.ProgramConfig;
            ASVStructureOptions mapOptions = new ASVStructureOptions()
            {
                Artifacts = config.Artifacts,
                BeaverDams = config.BeaverDams,
                BeeHives = config.BeeHives,
                ChargeNodes = config.ChargeNodes,
                DeinoNests = config.DeinoNests,
                DrakeNests = config.DrakeNests,
                GasVeins = config.GasVeins,
                Glitches = config.Glitches,
                MagmaNests = config.MagmaNests,
                OilVeins = config.OilVeins,
                Terminals = config.Obelisks,
                WaterVeins = config.WaterVeins,
                WyvernNests = config.WyvernNests
            };


            graphics = AddMapStructures(graphics, mapOptions);
            if (customMarkers != null && customMarkers.Count > 0)
            {
                graphics = AddCustomMarkers(graphics, customMarkers);
            }
            graphics = AddCurrentMarker(graphics, selectedLat, selectedLon);

            return bitmap;
        }

        private Graphics AddMapStructures(Graphics g, ASVStructureOptions structureOptions)
        {
            decimal markerX = 0;
            decimal markerY = 0;

            Tuple<int, int, decimal, decimal, decimal, decimal> mapvals = Tuple.Create(1024, 1024, 0.0m, 0.0m, 100.0m, 100.0m);

            //obelisks/tribute terminals
            if (structureOptions.Terminals)
            {
                var terminalMarkers = pack.TerminalMarkers;
                foreach (var terminal in terminalMarkers)
                {

                    //attempt to determine colour from class name
                    Color brushColor = Color.DarkGreen;

                    markerX = ((decimal)terminal.Longitude.GetValueOrDefault(0)) * 1024 / 100;
                    markerY = ((decimal)terminal.Latitude.GetValueOrDefault(0)) * 1024 / 100;


                    g.FillEllipse(new SolidBrush(brushColor), (float)markerX - 25f, (float)markerY - 25f, 50, 50);
                    g.DrawEllipse(new Pen(Color.White, 2), (float)markerX - 25f, (float)markerY - 25f, 50, 50);

                    Bitmap mapMarker = new Bitmap(ARKViewer.Properties.Resources.structure_marker_obelisk, new Size(40, 40));
                    g.DrawImage(mapMarker, (float)markerX - 20f, (float)markerY - 20f);


                }


                //TODO:// Get user defined terminals from json

            }

            if (structureOptions.Glitches)
            {
                foreach (var glitch in pack.GlitchMarkers)
                {
                    //attempt to determine colour from class name
                    Color brushColor = Color.MediumPurple;

                    markerX = ((decimal)glitch.Longitude.GetValueOrDefault(0)) * 1024 / 100;
                    markerY = ((decimal)glitch.Latitude.GetValueOrDefault(0)) * 1024 / 100;

                    float markerSize = 25;

                    g.FillEllipse(new SolidBrush(brushColor), (float)markerX - (markerSize / 2), (float)markerY - (markerSize / 2), markerSize, markerSize);
                    g.DrawEllipse(new Pen(Color.White, 2), (float)markerX - (markerSize / 2), (float)markerY - (markerSize / 2), markerSize, markerSize);

                    float iconSize = 18.75f;
                    Bitmap mapMarker = new Bitmap(ARKViewer.Properties.Resources.structure_marker_glitch, new Size((int)iconSize, (int)iconSize));
                    g.DrawImage(mapMarker, (float)markerX - (iconSize / 2), (float)markerY - (iconSize / 2));
                }
            }


            //charge nodes?
            if (structureOptions.ChargeNodes)
            {
                var chargeNodeList = pack.ChargeNodes;

                foreach (var chargeNode in chargeNodeList)
                {
                    markerX = ((decimal)chargeNode.Longitude.GetValueOrDefault(0)) * 1024 / 100;
                    markerY = ((decimal)chargeNode.Latitude.GetValueOrDefault(0)) * 1024 / 100;

                    g.FillEllipse(new SolidBrush(Color.White), (float)markerX - 15f, (float)markerY - 15f, 30, 30);

                    Color borderColor = Color.Red;
                    if (chargeNode.Inventory.Items.Count > 0)
                    {
                        borderColor = Color.Green;
                    }

                    g.DrawEllipse(new Pen(borderColor, 2), (float)markerX - 15f, (float)markerY - 15f, 30, 30);
                    Bitmap chargeMarker = new Bitmap(ARKViewer.Properties.Resources.structure_marker_battery, new Size(20, 20));
                    g.DrawImage(chargeMarker, (float)markerX - 10.0f, (float)markerY - 10.0f);
                }


            }


            //beaver dams
            if (structureOptions.BeaverDams)
            {
                var beaverDamList = pack.BeaverDams;
                foreach (var beaverDam in beaverDamList)
                {
                    markerX = ((decimal)beaverDam.Longitude.GetValueOrDefault(0)) * 1024 / 100;
                    markerY = ((decimal)beaverDam.Latitude.GetValueOrDefault(0)) * 1024 / 100;

                    g.FillEllipse(new SolidBrush(Color.White), (float)markerX - 15f, (float)markerY - 15f, 30, 30);

                    Color borderColor = Color.Red;
             
                    if (beaverDam.Inventory.Items.Count > 0)
                    {
                        borderColor = Color.Green;
                    }
                    g.DrawEllipse(new Pen(borderColor, 2), (float)markerX - 15f, (float)markerY - 15f, 30, 30);
                    Bitmap beaverMarker = new Bitmap(ARKViewer.Properties.Resources.structure_marker_beaver, new Size(20, 20));
                    g.DrawImage(beaverMarker, (float)markerX - 10.0f, (float)markerY - 10.0f);
                }
            }

            //deino nests
            if (structureOptions.DeinoNests)
            {
                var deinoNestList = pack.DeinoNests;
                foreach (var deinoNest in deinoNestList)
                {
                    markerX = ((decimal)deinoNest.Longitude.GetValueOrDefault(0)) * 1024 / 100;
                    markerY = ((decimal)deinoNest.Latitude.GetValueOrDefault(0)) * 1024 / 100;

                    g.FillEllipse(new SolidBrush(Color.White), (float)markerX - 15f, (float)markerY - 15f, 30, 30);


                    Color borderColor = Color.Red;
                    if (deinoNest.Inventory.Items.Count > 0)
                    {
                        borderColor = Color.Green;
                    }
                    g.DrawEllipse(new Pen(borderColor, 2), (float)markerX - 15f, (float)markerY - 15f, 30, 30);

                    Bitmap deinoMarker = new Bitmap(ARKViewer.Properties.Resources.structure_marker_deino, new Size(20, 20));
                    g.DrawImage(deinoMarker, (float)markerX - 10.0f, (float)markerY - 10.0f);

                }
            }

            //wyvern nests
            if (structureOptions.WyvernNests)
            {

                var wyvernNestList = pack.WyvernNests;
                foreach (var wyvernNest in wyvernNestList)
                {
                    markerX = ((decimal)wyvernNest.Longitude.GetValueOrDefault(0)) * 1024 / 100;
                    markerY = ((decimal)wyvernNest.Latitude.GetValueOrDefault(0)) * 1024 / 100;


                    g.FillEllipse(new SolidBrush(Color.White), (float)markerX - 15f, (float)markerY - 15f, 30, 30);

                    Color borderColor = Color.Red;
                    if (wyvernNest.Inventory.Items.Count > 0)
                    {

                        borderColor = Color.Green;
                    }


                    g.DrawEllipse(new Pen(borderColor, 2), (float)markerX - 15f, (float)markerY - 15f, 30, 30);

                    Bitmap wyvernMarker = new Bitmap(ARKViewer.Properties.Resources.structure_marker_wyvern, new Size(20, 20));
                    g.DrawImage(wyvernMarker, (float)markerX - 10.0f, (float)markerY - 10.0f);

                }
            }

            //rock drakes (RockDrakeNest_C)
            if (structureOptions.DrakeNests)
            {

                var drakeNestList = pack.DrakeNests; //gd.Structures.Where(f => f.ClassName == "RockDrakeNest_C");
                foreach (var drakeNest in drakeNestList)
                {

                    markerX = ((decimal)drakeNest.Longitude.GetValueOrDefault(0)) * 1024 / 100;
                    markerY = ((decimal)drakeNest.Latitude.GetValueOrDefault(0)) * 1024 / 100;


                    Color markerColor = Color.White;
                    g.FillEllipse(new SolidBrush(markerColor), (float)markerX - 15f, (float)markerY - 15f, 30, 30);


                    Color borderColor = Color.Red;
                    if (drakeNest.Inventory.Items.Count > 0)
                    {
                        borderColor = Color.Green;
                    }
                    g.DrawEllipse(new Pen(borderColor, 2), (float)markerX - 15f, (float)markerY - 15f, 30, 30);

                    Bitmap wyvernMarker = new Bitmap(ARKViewer.Properties.Resources.structure_marker_rockdrake, new Size(20, 20));
                    g.DrawImage(wyvernMarker, (float)markerX - 10.0f, (float)markerY - 10.0f);

                }
            }

            //magmasaur nests
            if (structureOptions.MagmaNests)
            {
                var magmaNests = pack.MagmaNests; //gd.Structures.Where(f => f.ClassName == "CherufeNest_C");
                foreach (var magmaNest in magmaNests)
                {

                    markerX = ((decimal)magmaNest.Longitude.GetValueOrDefault(0)) * 1024 / 100;
                    markerY = ((decimal)magmaNest.Latitude.GetValueOrDefault(0)) * 1024 / 100;

                    Color markerColor = Color.White;
                    g.FillEllipse(new SolidBrush(markerColor), (float)markerX - 15f, (float)markerY - 15f, 30, 30);

                    Color borderColor = Color.Red;

                    if (magmaNest.Inventory.Items.Count > 0)
                    {
                        borderColor = Color.Green;
                    }
                    g.DrawEllipse(new Pen(borderColor, 2), (float)markerX - 15f, (float)markerY - 15f, 30, 30);

                    Bitmap magmaMarker = new Bitmap(ARKViewer.Properties.Resources.structure_marker_magmasaur, new Size(20, 20));
                    g.DrawImage(magmaMarker, (float)markerX - 10.0f, (float)markerY - 10.0f);

                }
            }



            //oil veins (OilVein_)
            if (structureOptions.OilVeins)
            {
                var oilVeinList = pack.OilVeins; //gd.Structures.Where(f => f.ClassName.StartsWith("OilVein_"));
                foreach (var oilVein in oilVeinList)
                {
                    markerX = ((decimal)oilVein.Longitude.GetValueOrDefault(0)) * 1024 / 100;
                    markerY = ((decimal)oilVein.Latitude.GetValueOrDefault(0)) * 1024 / 100;

                    g.FillEllipse(new SolidBrush(Color.White), (float)markerX - 15f, (float)markerY - 15f, 30, 30);
                    g.DrawEllipse(new Pen(Color.Black, 2), (float)markerX - 15f, (float)markerY - 15f, 30, 30);

                    Bitmap oilMarker = new Bitmap(ARKViewer.Properties.Resources.structure_marker_oil, new Size(20, 20));
                    g.DrawImage(oilMarker, (float)markerX - 10.0f, (float)markerY - 10.0f);

                }

            }

            //water veins (WaterVein_)
            if (structureOptions.WaterVeins)
            {
                var waterVeinList = pack.WaterVeins; // gd.Structures.Where(f => f.ClassName.StartsWith("WaterVein_"));
                foreach (var waterVein in waterVeinList)
                {
                    markerX = ((decimal)waterVein.Longitude.GetValueOrDefault(0)) * 1024 / 100;
                    markerY = ((decimal)waterVein.Latitude.GetValueOrDefault(0)) * 1024 / 100;

                    g.FillEllipse(new SolidBrush(Color.White), (float)markerX - 15f, (float)markerY - 15f, 30, 30);
                    g.DrawEllipse(new Pen(Color.Black, 2), (float)markerX - 15f, (float)markerY - 15f, 30, 30);

                    Bitmap waterMarker = new Bitmap(ARKViewer.Properties.Resources.structure_marker_water, new Size(20, 20));
                    g.DrawImage(waterMarker, (float)markerX - 10.0f, (float)markerY - 10.0f);

                }

            }

            //gas veins (GasVein_)
            if (structureOptions.GasVeins)
            {
                var gasVeinList = pack.GasVeins; // gd.Structures.Where(f => f.ClassName.StartsWith("GasVein_"));
                foreach (var gasVein in gasVeinList)
                {
                    markerX = ((decimal)gasVein.Longitude.GetValueOrDefault(0)) * 1024 / 100;
                    markerY = ((decimal)gasVein.Latitude.GetValueOrDefault(0)) * 1024 / 100;

                    g.FillEllipse(new SolidBrush(Color.White), (float)markerX - 15f, (float)markerY - 15f, 30, 30);

                    Color borderColor = Color.WhiteSmoke;
                    g.DrawEllipse(new Pen(borderColor, 2), (float)markerX - 15f, (float)markerY - 15f, 30, 30);

                    Bitmap gasMarker = new Bitmap(ARKViewer.Properties.Resources.structure_marker_gas, new Size(20, 20));
                    g.DrawImage(gasMarker, (float)markerX - 10.0f, (float)markerY - 10.0f);

                }
            }

            //artifacts
            if (structureOptions.Artifacts)
            {
                var artifactList = pack.Artifacts; //gd.Structures.Where(f => f.ClassName.StartsWith("ArtifactCrate_"));
                foreach (var artifact in artifactList)
                {
                    markerX = ((decimal)artifact.Longitude.GetValueOrDefault(0)) * 1024 / 100;
                    markerY = ((decimal)artifact.Latitude.GetValueOrDefault(0)) * 1024 / 100;

                    g.FillEllipse(new SolidBrush(Color.FloralWhite), (float)markerX - 15.0f, (float)markerY - 15.0f, 30, 30);
                    g.DrawEllipse(new Pen(Color.Yellow, 1), (float)markerX - 15.0f, (float)markerY - 15.0f, 30, 30);

                    Bitmap gasMarker = new Bitmap(ARKViewer.Properties.Resources.structure_marker_trophy, new Size(20, 20));
                    g.DrawImage(gasMarker, (float)markerX - 10.0f, (float)markerY - 10.0f);

                }

            }

            if (structureOptions.BeeHives)
            {
                var hiveList = pack.BeeHives; 
                foreach (var hive in hiveList)
                {
                    markerX = ((decimal)hive.Longitude.GetValueOrDefault(0)) * 1024 / 100;
                    markerY = ((decimal)hive.Latitude.GetValueOrDefault(0)) * 1024 / 100;

                    g.FillEllipse(new SolidBrush(Color.FloralWhite), (float)markerX - 15.0f, (float)markerY - 15.0f, 30, 30);
                    g.DrawEllipse(new Pen(Color.Yellow, 1), (float)markerX - 15.0f, (float)markerY - 15.0f, 30, 30);

                    Bitmap hiveMarker = new Bitmap(ARKViewer.Properties.Resources.beehive, new Size(20, 20));
                    g.DrawImage(hiveMarker, (float)markerX - 10.0f, (float)markerY - 10.0f);

                }

            }




            return g;
        }

        //area, name tooltip
        public List<Tuple<RectangleF, string>> CustomMarkerRegions = new List<Tuple<RectangleF, string>>();

        private Graphics AddCustomMarkers(Graphics g, List<ContentMarker> markers)
        {
            CustomMarkerRegions = new List<Tuple<RectangleF,string>>();

            foreach (var marker in markers.Where(x=>x.Displayed))
            {
                var markerX = (decimal)(marker.Lon) * 1024 / 100;
                var markerY = (decimal)(marker.Lat) * 1024 / 100;

                Color markerBackGround = Color.FromArgb(marker.Colour);
                g.FillEllipse(new SolidBrush(markerBackGround), (float)markerX - 17.5f, (float)markerY - 17.5f, 35, 35);

                RectangleF markerRect = new RectangleF(new PointF((float)markerX - 17.5f, (float)markerY - 17.5f), new SizeF(35f, 35f));
                CustomMarkerRegions.Add(new Tuple<RectangleF, string>(markerRect,marker.Name));

                if (marker.Image.Length > 0)
                {
                    string imageFilename = Path.Combine(Program.MarkerImageFolder, marker.Image);
                    if (File.Exists(imageFilename))
                    {
                        Image markerImage = Image.FromFile(imageFilename);
                        g.DrawImage(new Bitmap(markerImage, 28, 28), (float)markerX - 14.0f, (float)markerY - 14.0f, 28.0f, 28.0f);
                    }
                }

                if (marker.BorderWidth > 0)
                {
                    Color markerBorder = Color.FromArgb(marker.BorderColour);
                    g.DrawEllipse(new Pen(markerBorder, marker.BorderWidth), (float)markerX - 17.5f, (float)markerY - 17.5f, 35, 35);
                }
            }
            return g;
        }
        private Graphics AddCurrentMarker(Graphics g, decimal? lat, decimal? lon)
        {

            if (lon != 0 && lat != 0)
            {

                var markerX = (decimal)(lon) * 1024 / 100;
                var markerY = (decimal)(lat) * 1024 / 100;
                Color markerColor = Color.Red;
                float markerSize = 20;

                g.FillEllipse(new SolidBrush(markerColor), (float)markerX - (markerSize / 2), (float)markerY - (markerSize / 2), markerSize, markerSize);

            }
            return g;
        }

        public  List<ContentPlayer> GetUploadedCharacters()
        {
            return pack.LocalProfile.UploadedCharacters;
        }

        public List<ContentTamedCreature> GetUploadedTames()
        {
            return pack.LocalProfile.UploadedTames;
        }

        public List<ContentLeaderboard> GetLeaderboards()
        {
            return pack.Leaderboards;
        }
    }
}
