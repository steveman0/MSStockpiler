using System;
using System.Text;
using UnityEngine;

public class MSStockpilerMod : FortressCraftMod
{

    public override ModRegistrationData Register()
    {
        ModRegistrationData modRegistrationData = new ModRegistrationData();
        modRegistrationData.RegisterEntityHandler("steveman0.MSStockpiler");


        Debug.Log("Mass Storage Stockpiler Port Mod registered");

        return modRegistrationData;
    }

    public override ModCreateSegmentEntityResults CreateSegmentEntity(ModCreateSegmentEntityParameters parameters)
    {
        ModCreateSegmentEntityResults result = new ModCreateSegmentEntityResults();

        foreach (ModCubeMap cubeMap in ModManager.mModMappings.CubeTypes)
        {
            if (cubeMap.CubeType == parameters.Cube)
            {
                if (cubeMap.Key.Equals("steveman0.MSStockpiler"))
                    result.Entity = new MSStockpiler(parameters.Segment, parameters.X, parameters.Y, parameters.Z, parameters.Cube, parameters.Flags, parameters.Value, parameters.LoadFromDisk);
            }
        }
        return result;
    }
}