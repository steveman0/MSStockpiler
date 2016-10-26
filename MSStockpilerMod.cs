using System;
using System.Text;
using UnityEngine;

public class MSStockpilerMod : FortressCraftMod
{
    public ushort StockpilerType = ModManager.mModMappings.CubesByKey["steveman0.MSStockpiler"].CubeType;

    public override ModRegistrationData Register()
    {
        ModRegistrationData modRegistrationData = new ModRegistrationData();
        modRegistrationData.RegisterEntityHandler("steveman0.MSStockpiler");
        modRegistrationData.RegisterEntityUI("steveman0.MSStockpiler", new MSStockpilerWindow());

        Debug.Log("Mass Storage Stockpiler Port Mod V11 registered");

        UIManager.NetworkCommandFunctions.Add("steveman0.MSStockpilerWindow", new UIManager.HandleNetworkCommand(MSStockpilerWindow.HandleNetworkCommand));

        return modRegistrationData;
    }

    public override ModCreateSegmentEntityResults CreateSegmentEntity(ModCreateSegmentEntityParameters parameters)
    {
        ModCreateSegmentEntityResults result = new ModCreateSegmentEntityResults();

        if (parameters.Cube == StockpilerType)
        {
            parameters.ObjectType = SpawnableObjectEnum.MassStorageInputPort;
            result.Entity = new MSStockpiler(parameters);
        }
        return result;
    }
}