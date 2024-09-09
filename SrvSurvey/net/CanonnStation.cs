﻿using DecimalMath;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using SrvSurvey.game;
using SrvSurvey.units;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace SrvSurvey.canonn
{
    internal class CanonnStation
    {
        #region data members

        public DateTimeOffset timestamp;
        public Version clientVer;

        // from various journal entries
        public string name;
        public long marketId;
        public long systemAddress;
        public int bodyId;
        public string stationEconomy;
        public StationType stationType;
        public double lat;
        public double @long;

        // calculated values
        public float heading = -1;
        public int subType;
        public CalcMethod calcMethod;

        // raw values for calculations
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public float cmdrHeading;
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public double cmdrLat;
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public double cmdrLong;
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public string cmdrShip;
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public int cmdrPad;
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public double bodyRadius;
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public LandingPads availblePads;

        #endregion

        public override string ToString()
        {
            return $"{name} ({marketId}/{systemAddress}/{bodyId})";
        }

        public static bool inferOdysseySettlementFromPads(CanonnStation station)
        {
            Game.log($"~~inferOdysseySettlementFromPads:\r\n" + JsonConvert.SerializeObject(station, Formatting.Indented));
            if (station.subType > 0) return false;

            // some subTypes can be inferred from just the economy and pad configuration...
            var mappingKey = $"{station.stationEconomy}/{station.availblePads}";
            station.subType = mapLandingPadConfigToSubType.GetValueOrDefault(mappingKey);
            if (station.subType > 0)
            {
                Game.log($"dockingRequested: {mappingKey} matched subType: #{station.subType}");
                station.calcMethod = CalcMethod.PadConfig;
                return true;
            }

            return false;
        }

        //public static void inferOdysseySettlementFromDocking(CanonnStation station, CalcMethod calcMethod)
        //{
        //    Game.log($"~~inferOdysseySettlementFromDocking: " + JsonConvert.SerializeObject(station, Formatting.Indented));
        //    if (station.subType > 0) return;

        //    var radius = (decimal)station.bodyRadius;
        //    var siteLocation = new LatLong2(station.lat, station.@long);
        //    var cmdrHeading = (float)station.cmdrHeading;

        //    var padLocation = adjustForCockpitOffset(radius, Status.here, station.cmdrShip, cmdrHeading);
        //    var stationEconomy = Util.toEconomy(station.stationEconomy);

        //    foreach (var template in HumanSiteTemplate.templates)
        //    {
        //        if (template.economy != stationEconomy) continue; // wrong economy
        //        if (station.availblePads.ToString() != template.landingPadSummary) continue; // wrong pad configuration

        //        // calculate distance from pad center
        //        var pad = template.landingPads[station.cmdrPad - 1];
        //        var shipOffset = Util.getOffset(radius, siteLocation, padLocation, cmdrHeading - pad.rot);

        //        var po = pad.offset - shipOffset;
        //        var distFromPadCenter = po.dist;

        //        // are we within ~5m of the expected pad? Is ~5m enough?
        //        if (distFromPadCenter < 50) // TODO: adjust by pad size?
        //        {
        //            station.subType = template.subType;
        //            station.calcMethod = calcMethod;
        //            Game.log($"~~inferOdysseySettlementFromDocking: matched {stationEconomy} #{station.subType} from pad #{station.cmdrPad}, shipDistFromPadCenter:" + Util.metersToString(distFromPadCenter));
        //            return;
        //        }
        //        else
        //        {
        //            // TODO: Remove once templates are populated
        //            Game.log($"~~not a match - {template.economy} #{template.subType}, pad #{station.cmdrPad} ({pad.size}): shipDistFromPadCenter: " + distFromPadCenter.ToString("N2"));
        //        }
        //    }

        //    if (station.subType == 0)
        //        Game.log($"inferOdysseySettlementFromDocking: Doh! We should have been able to infer the subType by this point :(");
        //}

        //public static void calculateOdysseySettlementHeadingFromDocking(CanonnStation station)
        //{
        //    if (station.subType == 0) return;

        //    if (station.cmdrPad == 1)
        //    {
        //        // pad 1 is always the definitive source of the site's heading
        //        station.heading = station.cmdrHeading;
        //    }
        //    else if (station.template == null)
        //    {
        //        Game.log($"Why no template already?!");
        //    }
        //    else
        //    {
        //        // for other pads, we must apply their rotation to the ships heading
        //        var padRot = station.template.landingPads[station.cmdrPad - 1].rot;
        //        station.heading = station.cmdrHeading - padRot;

        //        if (station.heading > 360) station.heading -= 360;
        //        if (station.heading < 0) station.heading += 360;
        //    }

        //    Game.log($"~~calculateOdysseySettlementHeadingFromDocking: new heading {station.heading}");
        //}

        public bool inferFromShip(float heading, double lat, double @long, string ship, double bodyRadius, CalcMethod calcMethod)
        {
            this.calcMethod = calcMethod;
            this.cmdrHeading = heading;
            this.cmdrLat = lat;
            this.cmdrLong = @long;
            this.cmdrShip = ship;
            this.bodyRadius = bodyRadius;
            this.timestamp = DateTimeOffset.UtcNow;

            return inferOdysseySettlementFromAnyPad(this);
        }

        public bool inferFromFoot(float heading, double lat, double @long, double bodyRadius)
        {
            this.calcMethod = CalcMethod.ManualFoot;
            this.cmdrPad = 0;
            this.cmdrHeading = heading;
            this.cmdrLat = lat;
            this.cmdrLong = @long;
            this.cmdrShip = "foot";
            this.bodyRadius = bodyRadius;
            this.timestamp = DateTimeOffset.UtcNow;

            return inferOdysseySettlementFromAnyPad(this);
        }

        public static bool inferOdysseySettlementFromAnyPad(CanonnStation station)
        {
            var wasHeadingUnknown = station.heading == -1;

            Game.log($"~~inferOdysseySettlementFromAnyPad:\r\n" + JsonConvert.SerializeObject(station, Formatting.Indented));
            var cmdrHeading = station.cmdrHeading;
            var bodyRadius = (decimal)station.bodyRadius;

            // offset cmdr location based on the ship they are in (or not if on foot)
            var cmdrLocation = new LatLong2(station.cmdrLat, station.cmdrLong);
            var padLocation = adjustForCockpitOffset(bodyRadius, cmdrLocation, station.cmdrShip, cmdrHeading);

            foreach (var template in HumanSiteTemplate.templates)
            {
                if (template.economy != station.economy) continue; // wrong economy
                if (station.availblePads != null && station.availblePads.ToString() != template.landingPadSummary) continue; // wrong pad configuration

                foreach (var pad in template.landingPads)
                {
                    var padIdx = template.landingPads.IndexOf(pad) + 1;

                    // skip if we had a target pad and this isn't it
                    if (station.cmdrPad > 0 && station.cmdrPad != padIdx) continue;

                    // measure distance 
                    var cmdrOffset = Util.getOffset(bodyRadius, station.location, padLocation, cmdrHeading - pad.rot);
                    var delta = pad.offset - cmdrOffset;

                    // are we within ~10m of the expected pad? Is ~5m enough?
                    if (delta.dist < 10) // TODO: adjust by pad size?
                    {
                        station.subType = template.subType;

                        var newHeading = 0f;
                        if (padIdx == 0)
                        {
                            newHeading = cmdrHeading - pad.rot;
                        }
                        else
                        {
                            // for other pads, we must apply their rotation to the ships heading
                            var padRot = pad.rot;
                            newHeading = cmdrHeading - padRot;
                        }
                        if (newHeading > 360) newHeading -= 360;
                        if (newHeading < 0) newHeading += 360;
                        Game.log($"~~inferOdysseySettlementFromAnyPad: marketId: {station.marketId} - matched {template.economy} #{template.subType} from pad #{padIdx}, station heading: {newHeading}°, distFromPadCenter: {delta.dist.ToString("N2")}m");

                        if (delta.dist > 5) Debugger.Break(); // why is the distance this high?

                        // only update heading if not previously known and we're not manually dockign at this time
                        if (wasHeadingUnknown || station.calcMethod != CalcMethod.ManualDock)
                            station.heading = newHeading;

                        if (wasHeadingUnknown)
                        {
                            // TODO: submit data if the calc method this time around is stronger than before?

                            // If we just calculated the heading for the first time - submit to Canonn
                            Game.canonn.submitStation(station).ContinueWith(response =>
                            {
                                Game.log("canonn.submitStation: " + response.Result);
                            });
                        }
                        return true;
                    }
                    else
                    {
                        // TODO: Remove once templates are populated
                        Game.log($"~~inferOdysseySettlementFromAnyPad: marketId: {station.marketId} - not a match - {template.economy} #{template.subType}, pad #{padIdx} ({pad.size}): distFromPadCenter: {delta.dist.ToString("N2")}m");
                    }
                }
            }

            if (station.subType == 0)
            {
                Game.log($"~~inferOdysseySettlementFromAnyPad: Doh! We should have been able to infer the subType by this point :(");
                Debugger.Break();
            }

            return false;
        }

        #region static mappings and helpers

        [JsonIgnore]
        public LatLong2 location { get => new LatLong2(this.lat, this.@long); }

        [JsonIgnore]
        public Economy economy { get => Util.toEconomy(this.stationEconomy); }

        [JsonIgnore]
        public HumanSiteTemplate? template { get => HumanSiteTemplate.get(this); }

        private static Dictionary<string, int> mapLandingPadConfigToSubType = new Dictionary<string, int>()
        {
            { "Agriculture/Small:2, Medium:0, Large:1", 4 },

            { "Military/Small:0, Medium:1, Large:0", 1 },
            { "Military/Small:2, Medium:0, Large:1", 2 },
            { "Military/Small:1, Medium:0, Large:0", 4 },

            { "Extraction/Small:0, Medium:0, Large:1", 3 },
            { "Extraction/Small:0, Medium:1, Large:0", 4 },
            { "Extraction/Small:1, Medium:0, Large:0", 5 },

            { "Industrial/Small:1, Medium:0, Large:0", 1 },
            { "Industrial/Small:0, Medium:1, Large:0", 3 },
            { "Industrial/Small:1, Medium:0, Large:1", 4 },

            { "HighTech/Small:1, Medium:0, Large:1", 1 },
            { "HighTech/Small:3, Medium:0, Large:0", 4 },

            { "Tourist/Small:2, Medium:0, Large:2", 1 },
            { "Tourist/Small:0, Medium:2, Large:0", 2 },
            { "Tourist/Small:2, Medium:0, Large:1", 3 },
            { "Tourist/Small:1, Medium:0, Large:1", 4 },
        };

        public static LatLong2 adjustForCockpitOffset(decimal bodyRadius, LatLong2 location, string shipType, float shipHeading)
        {
            if (shipType == null || shipType == "foot") return location;

            var offset = mapShipCockpitOffsets.GetValueOrDefault(shipType);
            if (offset.X == 0 && offset.Y == 0) return location;

            var pd = offset.rotate((decimal)shipHeading);

            var dpm = 1 / (DecimalEx.TwoPi * bodyRadius / 360m); // meters per degree
            var dx = pd.X * dpm;
            var dy = pd.Y * dpm;

            var newLocation = new LatLong2(
                location.Lat + dy,
                location.Long + dx
            );

            var dist = offset.dist;
            var angle = offset.angle + (decimal)shipHeading;
            Game.log($"Adjusting landing location for: {shipType}, dist: {dist}, angle: {angle}\r\npd:{pd}, po:{offset} (alt: {Game.activeGame?.status?.Altitude})\r\n{location} =>\r\n{newLocation}");
            return newLocation;
        }

        /// <summary>
        /// Per ship type, the relative location, in meters, of the cockpit to the center of the ship.
        /// </summary>
        private static Dictionary<string, PointM> mapShipCockpitOffsets = new Dictionary<string, PointM>()
        {
            // Determined by measuring difference between Docked lat/long and pad center lat/long, converted to meters.
            { "sidewinder", new PointM(0.0039735241560325261715803963, -1.8918079917214574007873715993) }, // Sidewinder
            { "eagle", new PointM(0.2022841743348611074877144011, -9.475366622689792311585338976) }, // Eagle
            { "hauler", new PointM(0.0969766998443601240987358377, -12.599239384408765342054135780) }, // Hauler
            { "adder", new PointM(-1.0448119356622934101273911152, -11.715904797681276720908616160) }, // Adder
            { "empire_eagle", new PointM(0.2458336285994541250352346834, -8.536714551074841702761431858) }, // Imperial Eagle
            { "viper", new PointM(0.1229161697432489848756919882, -7.1826149264813328843504872434) }, // Viper mk3
            { "cobramkiii", new PointM(-0.1576497087354904399530401764, -9.031276393889643127040323461) }, // Cobra mk3
            { "viper_mkiv", new PointM(-0.0000027673979308549679875255, -8.065723234733315600077184502) }, // Viper mk4
            { "diamondback", new PointM(-0.0000041614913376638187396197, -9.890813997121282004764478288) }, // Diamondback Scout
            // TODO: Cobra mk4
            { "type6", new PointM(0, -20.957581116002204026636899079) }, // Type 6
            { "dolphin", new PointM(0.24276978, -19.054316) }, // Dolphin
            { "diamondbackxl", new PointM(0.5462154, -18.501362) }, // Diamondback Explorer
            { "empire_courier", new PointM(0, -14.442907807215595156071678409) }, // Imperial Courier
            { "independant_trader", new PointM(0, -21.080499480318932414951958905) }, // Keelback
            { "asp_scout", new PointM(0.2362447906633601607725471808, -24.017676273766910604551594809) }, // Asp Scout
            { "vulture", new PointM(0.2458256590423615571253037271, -16.131446806855020630091723412) }, // Vulture
            { "asp", new PointM(0, -25.075346320612607494013645436) }, // Asp Explorer
            { "federation_dropship", new PointM(0.0330741525538768991029085273, -34.466606354146648479928088880) }, // Federal Dropship
            { "type7", new PointM(-0.3844698663548396996706138817, -36.259122767921040459540410044) }, // Type 7
            { "typex", new PointM(0, -26.120152417304799609067982615) }, // Alliance Chieftain
            { "federation_dropship_mkii", new PointM(-0.0336081918131731271433146967, -34.484471546920929254481108532) }, // Federal Assault ship
            { "empire_trader", new PointM(-1.6299010929385204745138544910, -42.459299074535490986652874405) }, // Imperial Clipper
            { "typex_2", new PointM(-0.1340211463206371812908012133, -25.743781130382646599249896967) }, // Alliance Crusader
            { "typex_3", new PointM(-0.1499968019864161648299420609, -23.326543081110057742124991058) }, // Alliance Challenger
            { "federation_gunship", new PointM(-0.0336081918131731271433146967, -34.484471546920929254481108532) }, // Federal Gunship
            { "krait_light", new PointM(0.6031567730891498506706891727, -29.808101534971506981430079248) }, // Krait Phantom
            { "krait_mkii", new PointM(-0.4390055060502030101940869492, -28.642501220707376952320201527) }, // Krait mk2
            { "orca", new PointM(1.8251181979081937256954303615, -60.721671830785327505179094956) }, // Orca
            { "ferdelance", new PointM(-1.2886041335053920724479975148, -11.051961482268357729454057254) }, // Fer-de-lance
            { "mamba", new PointM(-0.3384479441319697171254213427, -17.016087432359903431927706572) }, // Mamba
            { "python", new PointM(0.0242815204676919790218357202, -27.803238864751802112883958858) }, // Python
            { "python_nx", new PointM(-0.1985071274895448505330679553, -27.652575857555383324975206283) }, // Python mk2
            { "type9", new PointM(0, -41.976621414162772124132252950) }, // Type 9
            { "belugaliner", new PointM(-0.1590069086272495414747604156, -96.06768779190352971899572620) }, // Beluga
            { "type9_military", new PointM(0, -41.976621414162772124132252950) }, // Type 10
            { "anaconda", new PointM(-0.2973854218978083346300022763, 11.835423460533919103569434241) }, // Anaconda
            { "federation_corvette", new PointM(0, 17.577326097292171045687273834) }, // Federal Corvette
            { "cutter", new PointM(0, -78.975049073498041219641152452) }, // Imperial Cutter

            { "foot", new PointM(0d, 0d) }, // No offset applied when on foot
            { "taxi", new PointM(+1.0448119356622934101273911152, -11.715904797681276720908616160) }, // Taxi is an Adder but with X inverted
        };

        #endregion

    }

    [JsonConverter(typeof(StringEnumConverter))]
    internal enum CalcMethod
    {
        Unknown,
        /// <summary> Subtyped was matched from known landing pad configurations </summary>
        PadConfig,
        /// <summary> Subtyped was matched on Docking event using auto-dock </summary>
        AutoDock,
        /// <summary> Subtyped was matched on Docking event without auto-dock </summary>
        ManualDock,
        /// <summary> Subtyped was matched from cmdr standing in the middle of some landing pad </summary>
        ManualFoot,
        /// <summary> Subtyped was matched on Docking event when in a Taxi </summary>
        TaxiDock,
    }
}
