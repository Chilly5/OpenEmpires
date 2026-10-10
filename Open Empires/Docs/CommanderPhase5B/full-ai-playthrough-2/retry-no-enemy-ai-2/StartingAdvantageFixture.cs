// Non-shipping, one-shot experiment setup. Compile in memory, outside Assets.
using System;
using System.Linq;
using System.Reflection;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using OpenEmpires;

public static class Playthrough2NoEnemySecondStartingAdvantage
{
    static bool armed;
    public static object Arm()
    {
        if (GameBootstrapper.Instance.Simulation != null || armed) throw new InvalidOperationException("Arm only before the match.");
        FogOfWarRenderer.RevealUnexplored = false;
        FogOfWarRenderer.DisableFogOfWar = false;
        armed = true;
        EditorApplication.update += Initialize;
        return new { armed=true, extraSpearmen=20, completedHouses=3, terrainReveal=false };
    }
    static void Initialize()
    {
        var bootstrap=GameBootstrapper.Instance;
        if (bootstrap == null || bootstrap.Simulation == null) return;
        EditorApplication.update -= Initialize; armed=false;
        var sim=bootstrap.Simulation;
        var report=new JObject { ["utc"]=DateTime.UtcNow.ToString("O"), ["setup_tick"]=sim.CurrentTick,
            ["method"]="One-shot startup fixture; normal debug unit factory; three completed Houses", ["houses"]=new JArray(), ["spearmen"]=new JArray() };
        try
        {
            if(sim.CurrentTick>30) throw new InvalidOperationException("Startup window missed; no mutation permitted.");
            var aiList=(System.Collections.Generic.List<AIPlayerSystem>)typeof(GameSimulation).GetField("aiPlayers",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(sim); report["enemy_ai_before"]=aiList.Count; aiList.Clear(); report["enemy_ai_after"]=aiList.Count; var tc=sim.BuildingRegistry.GetAllBuildings().First(b=>b.PlayerId==0 && b.Type==BuildingType.TownCenter);
            report["resources_before"]=JObject.FromObject(sim.ResourceManager.GetPlayerResources(0));
            report["population_before"]=sim.GetPopulation(0); report["cap_before"]=sim.GetPopulationCap(0);
            int houses=0;
            for(int radius=6;radius<=16 && houses<3;radius++)
                for(int dx=-radius;dx<=radius && houses<3;dx++)
                    for(int dz=-radius;dz<=radius && houses<3;dz++)
                    {
                        if(Math.Abs(dx)!=radius && Math.Abs(dz)!=radius) continue;
                        int x=tc.OriginTileX+dx,z=tc.OriginTileZ+dz;
                        bool valid=true;
                        for(int xx=x;xx<x+2;xx++) for(int zz=z;zz<z+2;zz++) if(!sim.MapData.IsBuildable(xx,zz)) valid=false;
                        if(!valid) continue;
                        var b=sim.CreateBuilding(0,BuildingType.House,x,z,false);
                        ((JArray)report["houses"]).Add(new JObject { ["id"]=b.Id,["x"]=x,["z"]=z });houses++;
                    }
            if(houses!=3) throw new InvalidOperationException("Could not create exactly three safe Houses.");
            var spawn=typeof(GameSimulation).GetMethod("ProcessCheatSpawnUnitCommand",BindingFlags.Instance|BindingFlags.NonPublic);
            int count=0;
            for(int radius=3;radius<=10 && count<20;radius++)
                for(int dx=-radius;dx<=radius && count<20;dx++)
                    for(int dz=-radius;dz<=radius && count<20;dz++)
                    {
                        if(Math.Abs(dx)!=radius && Math.Abs(dz)!=radius) continue;
                        int x=tc.OriginTileX+dx,z=tc.OriginTileZ+dz;
                        if(!sim.MapData.IsWalkable(x,z) || sim.UnitRegistry.GetAllUnits().Any(u=>sim.MapData.WorldToTile(u.SimPosition)==new Vector2Int(x,z))) continue;
                        var pos=sim.MapData.TileToWorldFixed(x,z);
                        spawn.Invoke(sim,new object[]{new CheatSpawnUnitCommand(0,1,pos,1,0)});
                        var unit=sim.UnitRegistry.GetAllUnits().Last(u=>u.PlayerId==0 && u.UnitType==1);
                        ((JArray)report["spearmen"]).Add(new JObject { ["id"]=unit.Id,["x"]=x,["z"]=z,["health"]=unit.CurrentHealth });count++;
                    }
            if(count!=20) throw new InvalidOperationException("Could not create exactly twenty safe Spearmen.");
            report["resources_after"]=JObject.FromObject(sim.ResourceManager.GetPlayerResources(0));
            report["population_after"]=sim.GetPopulation(0);report["cap_after"]=sim.GetPopulationCap(0);
            report["success"]=true;
            PassivePlaythrough2NoEnemySecondObserver200.Attach(200);
        }
        catch(Exception e){report["success"]=false;report["error"]=e.ToString();}
        string path=Path.Combine(Path.GetDirectoryName(Application.dataPath),"Docs/CommanderPhase5B/full-ai-playthrough-2/retry-no-enemy-ai-2/startup-advantage.json");
        File.WriteAllText(path,report.ToString(Formatting.Indented));
    }
}


