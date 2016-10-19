using UnityEngine;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using FortressCraft.Community.Utilities;


public class MSStockpiler : MachineEntity, ItemConsumerInterface, ItemConfigurerInterface
{
    private float mrDroneTargetHeight = 0.25f;
    public float mrMaxSearchDistance = 10f;
    public MSStockpiler.eState meState;
    public MassStorageCrate mAttachedCrate;
    private MassStorageCrate mTargetCrate;
    public MachineInventory LocalInventory;
    public MachineInventory DroneInventory;
    public float mrCarryTimer;
    public float mrCarryDistance;
    private float mrDroneHeight;
    private Vector3 mForwards;
    public float mrStateTimer;
    private bool mbLinkedToGO;
    private GameObject CarryDrone;
    private GameObject CarryDroneClamp;
    private GameObject mCarriedObjectItem;
    private GameObject InputHopperObject;
    private Segment mDroneSegment;
    private bool mbCarriedCubeNeedsConfiguring;
    private Vector3 mUnityDronePos;
    private Vector3 mUnityDroneTarget;
    private Vector3 mUnityDroneRestPos;
    public int QuantityToStock;
    public ItemBase ItemToStock;
    private float PopupDebounce = 0.0f;
    public int CurrentStock = 999999;
    private float StockRefresh = 0.0f;
    private bool LocalCrateLockOut = false;
    private float CrateLockTimer = 0.0f;
    public int[] IndexedDistances;
    public int Tier = 0;
    private GameObject DigiTransmit;
    public MSStockpiler.eState meUnityState;
    private static ushort MK1_Value = ModManager.mModMappings.CubesByKey["steveman0.MSStockpiler"].ValuesByKey["steveman0.MSStockpilerMK1"].Value;
    private static ushort MK2_Value = ModManager.mModMappings.CubesByKey["steveman0.MSStockpiler"].ValuesByKey["steveman0.MSStockpilerMK2"].Value;
    private static ushort MK3_Value = ModManager.mModMappings.CubesByKey["steveman0.MSStockpiler"].ValuesByKey["steveman0.MSStockpilerMK3"].Value;
    public MSStockpilerWindow MachineWindow = new MSStockpilerWindow();
    public bool InventoryLock = false;

    public MSStockpiler(Segment segment, long x, long y, long z, ushort cube, byte flags, ushort lValue, bool fromDisk)
    : base(eSegmentEntity.Mod, SpawnableObjectEnum.MassStorageInputPort, x, y, z, cube, flags, lValue, Vector3.zero, segment)
  {
        this.meState = MSStockpiler.eState.Unknown;
        this.mbNeedsLowFrequencyUpdate = true;
        this.mbNeedsUnityUpdate = true;
        this.mForwards = SegmentCustomRenderer.GetRotationQuaternion(flags) * Vector3.forward;
        this.mForwards.Normalize();
        this.mrMaxSearchDistance = 1f;
        if (lValue == MK1_Value)
            this.Tier = 1;
        else if (lValue == MK2_Value)
            this.Tier = 2;
        else if (lValue == MK3_Value)
            this.Tier = 3;
        switch (this.Tier)
        {
            case 1:
                this.LocalInventory = new MachineInventory(this, 25);
                this.DroneInventory = new MachineInventory(this, 5);
                break;
            case 2:
                this.LocalInventory = new MachineInventory(this, 50);
                this.DroneInventory = new MachineInventory(this, 12);
                break;
            case 3:
                this.LocalInventory = new MachineInventory(this, 100);
                this.DroneInventory = new MachineInventory(this, 25);
                break;
            default:
                this.LocalInventory = new MachineInventory(this, 1);
                this.DroneInventory = new MachineInventory(this, 1);
                Debug.LogWarning("Error, MSStockpiler tier not recognized! " + lValue + " " + this.Tier);
                break;
        }
    }

    public override string GetPopupText()
    {
        //For compatibility with UI mods
        //UIUtil.EscapeUI();

        //Actual UI implementation
        UIUtil.HandleThisMachineWindow(this, MachineWindow);

        MSStockpiler port = (MSStockpiler)WorldScript.instance.localPlayerInstance.mPlayerBlockPicker.selectedEntity;
        string lstr1;
        switch (this.Tier)
        {
            case 1:
                lstr1 = "Mass Storage Stockpiler Port MK1\n";
                break;
            case 2:
                lstr1 = "Mass Storage Stockpiler Port MK2\n";
                break;
            case 3:
                lstr1 = "Mass Storage Stockpiler Port MK3\n";
                break;
            default:
                lstr1 = "Error machine tier not recognized!";
                break;
        }
        switch (this.meState)
        {
            case MSStockpiler.eState.LookingForAttachedStorage:
                lstr1 += "Looking for Attached Storage\n";
                break;
            case MSStockpiler.eState.SendingDrone:
                lstr1 += "Sending Drone\n";
                break;
            case MSStockpiler.eState.RetrievingDrone:
                lstr1 += "Retrieving Drone\n";
                break;
            case MSStockpiler.eState.SearchingForStorage:
                lstr1 += "Searching for Storage\n";
                break;
            case MSStockpiler.eState.AwaitingForLowStock:
                lstr1 += "Awaiting Low Stock\n";
                break;
            default:
                lstr1 += this.meState + "\n";
                break;
        }
        string lstr2 = "";
        string lstr3 = "";
        string lstr4 = "";
        int lnAvailable = 0;
        ItemBase itemtostock = this.GetCurrentHotBarItemOrCubeAsItem(out lnAvailable, false);

        if (this.mAttachedCrate == null)
        {
            lstr3 = "Looking for mass storage crate...\n";
        }
        else if (this.IndexedDistances == null)
        {
            lstr3 = "Surveying mass storage crate distances...\n";
        }
        else
        {
            if (itemtostock != null && itemtostock.mnItemID == -1 && itemtostock.mType != ItemType.ItemCubeStack)
            {
                Debug.LogWarning(("Error, ItemID was " + itemtostock.mnItemID));
                itemtostock = (ItemBase)null;
            }
            //Check assigned Item for stocking and display status
            //if (this.ItemToStock != null)
            //    lstr2 = !WorldScript.mLocalPlayer.mResearch.IsKnown(this.ItemToStock) ? "Currently stocking : Unknown Material\n" : "Currently stocking : " + ItemManager.GetItemName(this.ItemToStock) + "\n";
            //Check current hotbar item and display ability to set stockpile target
            //lstr3 = itemtostock != null ? (!WorldScript.mLocalPlayer.mResearch.IsKnown(itemtostock) ? lstr2 + "Press T to set Stockpile target to Unknown Material" : (lstr2 + "Press T to set Stockpile target to " + ItemManager.GetItemName(itemtostock)) + "\n") : (this.ItemToStock != null ? lstr2 + "Press T to clear Stockpile target\n" : lstr2 + "Select an item in your Hotbar to set Stockpile target\n");
            //if (Input.GetButtonDown("Store") && MSStockpilerWindow.SetItemToStock(WorldScript.mLocalPlayer, this, itemtostock) && this.PopupDebounce < 0 && UIManager.AllowInteracting)
            //{
            //    if (itemtostock == null)
            //    {
            //        AudioHUDManager.instance.HUDOut();
            //        Debug.Log((object)"Stockpiler Port itemtostock cleared");
            //    }
            //    else
            //    {
            //        AudioHUDManager.instance.HUDIn();
            //        Debug.LogWarning((object)("Set Stockpiler to " + ItemManager.GetItemName(itemtostock)));
            //    }
            //    this.PopupDebounce = 0.3f;
            //}
            if (this.ItemToStock != null)
            {
                lstr3 = !WorldScript.mLocalPlayer.mResearch.IsKnown(this.ItemToStock) ? "Currently stocking : Unknown Material\n" : "Currently stocking : " + ItemManager.GetItemName(this.ItemToStock) + "\n";
                lstr4 = "Stockpile quantity: " + this.CurrentStock + "/" + this.QuantityToStock  + "\nPress E to configure Stock limits\n";
                //int amount = 0;
                //if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                //    amount = 10;
                //else if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
                //    amount = 1;
                //else if (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt))
                //    amount = 1000;
                //else
                //    amount = 100;
                //lstr4 += amount + "\n";
                //if (this.PopupDebounce < 0 && Input.GetButtonDown("Interact") && UIManager.AllowInteracting)
                //{
                //    MSStockpilerWindow.SetStockpile(WorldScript.mLocalPlayer, port, this.QuantityToStock + amount);
                //    this.PopupDebounce = 0.3f;
                //}
                //if (this.PopupDebounce < 0 && Input.GetButtonDown("Extract") && UIManager.AllowInteracting)
                //{
                //    MSStockpilerWindow.SetStockpile(WorldScript.mLocalPlayer, port, this.QuantityToStock - amount);
                //    this.PopupDebounce = 0.3f;
                //}
            }
            else
            {
                lstr3 = "Press E to configure item to stock\n";
            }
            this.PopupDebounce -= Time.deltaTime;
        }

        int localcount = 0;
        int dronecount = 0;
        if (this.LocalInventory.Inventory != null && !this.LocalInventory.IsEmpty())
            localcount = this.LocalInventory.ItemCount();
        if (this.DroneInventory.Inventory != null && !this.DroneInventory.IsEmpty())
            dronecount = this.DroneInventory.ItemCount();

        string lstr5 = "Local inventory: " + localcount.ToString("N0") + "\nDrone inventory: " + dronecount.ToString("N0");
        string lstr9 = string.Concat(new object[4]
            {
                lstr1,
                lstr3,
                lstr4,
                lstr5
            });
        return (lstr9);
    }

    public void SetItemToStock(ItemBase lItem)
    {
        if (this.mTargetCrate != null && (double)this.mTargetCrate.mrInputLockTimer > 0.0)
            Debug.LogWarning((object)"Warning, changing itemtostock whilst we have a TargetCrate locked!");
        if (this.ItemToStock == null || lItem == null)
        {
            //this.mbHoloPreviewDirty = true;
            if (lItem == null)
                Debug.Log((object)"MSOP Cleared itemtostock");
        }
        //else
        //{
        //    if (lItem.mnItemID == this.ItemToStock.mnItemID)
        //        return;
        //    //this.mbHoloPreviewDirty = true;
        //    //Debug.Log((object)("MSOP Set itemtostock to " + ItemManager.GetItemName(lItem.mnItemID)));
        //}
        this.ItemToStock = lItem;
        //if (this.ItemToStock.mType == ItemType.ItemCubeStack)
        //    Debug.Log("This item to stock set as cube with ID: " + (this.ItemToStock as ItemCubeStack).mCubeType + " and value: " + (this.ItemToStock as ItemCubeStack).mCubeValue);
        MSStockpiler port = (MSStockpiler)WorldScript.instance.localPlayerInstance.mPlayerBlockPicker.selectedEntity;
        //MSStockpilerWindow.SetStockpile(WorldScript.mLocalPlayer, port, 0);
        this.QuantityToStock = 0;
        this.MarkDirtyDelayed();
    }

    public void SetStockpile(int stockpile)
    {
        this.QuantityToStock = stockpile;
        if (this.QuantityToStock < 0)
            this.QuantityToStock = 0;
    }

    public override void LowFrequencyUpdate()
    {
        if (GameState.PlayerSpawnedAndHadUpdates)
        {
            long x;
            long y;
            long z;
            WorldScript.instance.mPlayerFrustrum.GetCoordsFromUnityWithRoundingFix(this.mUnityDronePos, out x, out y, out z);
            this.mDroneSegment = this.AttemptGetSegment(x, y, z);
        }

        this.StockRefresh -= LowFrequencyThread.mrPreviousUpdateTimeStep;

        if (this.mAttachedCrate != null)
        {
            if (this.mAttachedCrate.mbDelete || this.mAttachedCrate != this.mAttachedCrate.GetCenter())
                this.mAttachedCrate = null;
        }
        //if (this.LocalInventory.HasSpareCapcity())
        //{
        //    ItemBase item = this.TakeFromSurrounding();
        //    if (item != null)
        //        this.LocalInventory.AddItem(item);
        //}
        if (this.meState == MSStockpiler.eState.Unknown)
            this.SetNewState(MSStockpiler.eState.LookingForAttachedStorage);
        if (this.meState == MSStockpiler.eState.LookingForAttachedStorage)
            this.LookForAttachedStorage();
        if (this.meState == MSStockpiler.eState.AwaitingForLowStock && this.ItemToStock == null)
            this.SetNewState(eState.Idling);
        if (this.mAttachedCrate == null)
        {
            this.SetNewState(MSStockpiler.eState.LookingForAttachedStorage);
        }
        else
        {
            if (this.meState == MSStockpiler.eState.SendingDrone)
            {
                if (this.DroneInventory.IsEmpty())
                    Debug.LogError((object)"Error, trying to send drone, but not carrying an item");
                if (this.mTargetCrate == null)
                    Debug.LogError((object)"Error, trying to send drone, but TargetCrate is null!");
                if ((double)this.mrStateTimer == 0.0)
                {
                    this.mUnityDroneTarget = WorldScript.instance.mPlayerFrustrum.GetCoordsToUnity(this.mTargetCrate.mnX, this.mTargetCrate.mnY, this.mTargetCrate.mnZ) + new Vector3(0.5f, 1.5f, 0.5f);
                    this.mrCarryDistance = 5f;
                    this.mrCarryTimer = this.mrCarryDistance;
                }
                this.mrCarryTimer -= LowFrequencyThread.mrPreviousUpdateTimeStep;
                if ((double)this.mrCarryTimer < 0.0)
                {
                    //this.mTargetCrate.AddItem(this.DroneInventory.RemoveAnySingle());
                    this.HandleItemAdd();
                    this.mTargetCrate.mrInputLockTimer = 0.0f;
                    this.mrCarryTimer = 1f;
                    this.mrCarryTimer = this.mrCarryDistance;
                    this.mbCarriedCubeNeedsConfiguring = true;
                    int loop = this.mTargetCrate.mnLocalFreeStorage;
                    if (this.mTargetCrate.mMode == MassStorageCrate.CrateMode.Items && !this.DroneInventory.IsEmpty())
                    {
                        for (int index = 0; index < this.DroneInventory.Inventory.Count; index++)
                        {
                            if (this.mTargetCrate.mnLocalFreeStorage > 0)
                            {
                                ItemBase item = this.DroneInventory.Inventory[index];
                                if (!item.IsStack() && item != null)
                                {
                                    if (!this.mTargetCrate.AddItem(this.DroneInventory.RemoveItem(item)))
                                        this.DroneInventory.AddItem(item);
                                    else
                                        index--;
                                    // this.mTargetCrate.AddItem(ItemBaseUtil.RemoveListItem(item, ref this.DroneInventory.Inventory, false));
                                }
                            }
                            else
                                break;
                        }
                    }
                    //Else go to a new crate if items remain in inventory
                    if (!this.DroneInventory.IsEmpty())
                    {
                        if (this.GetCrate(this.DroneInventory.Inventory[0]))
                        {
                            this.mUnityDroneTarget = WorldScript.instance.mPlayerFrustrum.GetCoordsToUnity(this.mTargetCrate.mnX, this.mTargetCrate.mnY, this.mTargetCrate.mnZ) + new Vector3(0.5f, 1.5f, 0.5f);
                            this.mrCarryDistance = 5f;
                            this.mrCarryTimer = this.mrCarryDistance;
                        }
                        else
                        {
                            this.mrCarryDistance = 5f;
                            this.mrCarryTimer = this.mrCarryDistance;
                            this.mTargetCrate = (MassStorageCrate)null;
                            this.SetNewState(MSStockpiler.eState.RetrievingDrone);
                        }
                    }
                    else
                    {
                        this.mrCarryDistance = 5f;
                        this.mrCarryTimer = this.mrCarryDistance;
                        this.mTargetCrate = (MassStorageCrate)null;
                        this.SetNewState(MSStockpiler.eState.RetrievingDrone);
                    }
                }
                this.mrStateTimer += LowFrequencyThread.mrPreviousUpdateTimeStep;
            }
            if (this.meState == MSStockpiler.eState.RetrievingDrone)
            {
                this.mrCarryTimer -= LowFrequencyThread.mrPreviousUpdateTimeStep;
                if ((double)this.mrCarryTimer < 0.0)
                    this.SetNewState(MSStockpiler.eState.Idling);
                this.mrStateTimer += LowFrequencyThread.mrPreviousUpdateTimeStep;
            }
            //Check if we've indexed the mass storage crate distances or they've changed and update the list as needed
            if (IndexedDistances == null)
            {
                if (this.HandleCrateLockOut())
                    return;
            }
            else if (IndexedDistances.Count() != mAttachedCrate.mConnectedCrates.Count + 1 || LocalCrateLockOut)
            {
                if (!LocalCrateLockOut)
                    this.IndexedDistances = null;
                if (this.HandleCrateLockOut())
                    return;
            }
            //Check if we're due to refresh the inventorying of the stockpiled item
            if (this.StockRefresh < 0 && this.ItemToStock != null)
            {
                this.CountUpStockpile();
                //Set refresh rate based on stock larger stock can be more granular to save CPU cycles
                this.StockRefresh = this.QuantityToStock < 50 ? 1.0f : this.QuantityToStock < 1000 ? 3.0f : 5.0f;
                if (this.meState == MSStockpiler.eState.AwaitingForLowStock && this.CurrentStock >= this.QuantityToStock)
                    return;
                else if (this.meState == MSStockpiler.eState.AwaitingForLowStock && this.CurrentStock < this.QuantityToStock)
                    this.SetNewState(MSStockpiler.eState.Idling);
            }
            if (this.meState == MSStockpiler.eState.Idling)
            {
                if (!this.DroneInventory.IsFull() && !this.LocalInventory.IsEmpty() && (this.CurrentStock < this.QuantityToStock || this.ItemToStock == null))
                    this.DroneInventory.Fill(ref this.LocalInventory.Inventory);
                else if (!this.DroneInventory.IsFull() && !this.LocalInventory.IsEmpty() && this.ItemToStock != null)
                    this.DroneInventory.FillBlackList(ref this.LocalInventory.Inventory, this.ItemToStock);
                if (!this.DroneInventory.IsEmpty())
                {
                    this.SetNewState(MSStockpiler.eState.SearchingForStorage);
                    this.mbCarriedCubeNeedsConfiguring = true;
                }
                else if (this.LocalInventory.IsFull() || (this.InventoryLock && this.CurrentStock >= this.QuantityToStock))
                    this.SetNewState(MSStockpiler.eState.AwaitingForLowStock);
                this.mrStateTimer += LowFrequencyThread.mrPreviousUpdateTimeStep;
            }
            if (this.meState == MSStockpiler.eState.SearchingForStorage)
            {
                this.mrStateTimer += LowFrequencyThread.mrPreviousUpdateTimeStep;
                if (this.DroneInventory.IsEmpty())
                {
                    Debug.LogWarning((object)"Error, MSIP SearchingForStorage but not carrying item?!");
                    this.SetNewState(MSStockpiler.eState.Idling);
                    return;
                }
                if (this.mAttachedCrate == null)
                    return;
                if (!GetCrate(this.DroneInventory.Inventory[0]))
                    return;
            }
        }
    }

    private bool GetCrate(ItemBase item)
    {
        if (this.mAttachedCrate == null || this.mAttachedCrate.mConnectedCrates == null || this.mAttachedCrate != this.mAttachedCrate.GetCenter() || item == null || this.IndexedDistances == null)
            return false;
        int count = this.mAttachedCrate.mConnectedCrates.Count;
        int crateindex;
        for (int index = 0; index < count + 1; ++index)
        {
            try
            {
                crateindex = IndexedDistances[index];
            }
            catch (System.IndexOutOfRangeException)
            {
                this.SetNewState(MSStockpiler.eState.Idling);
                Debug.Log("Mass Storage changed while we were trying to access it!");
                IndexedDistances = null;
                return false;
            }
            if (crateindex == count)
            {
                if (this.AssignTargetCrateIfFree(this.mAttachedCrate, item))
                    return true;
            }
            else
            {
                if (this.AssignTargetCrateIfFree(this.mAttachedCrate.mConnectedCrates[crateindex], item))
                    return true;
            }
        }
        return false;
    }

    //Modified to allow for partial stack fill
    private bool AssignTargetCrateIfFree(MassStorageCrate lCrate, ItemBase carriedItem)
    {
        if (carriedItem == null || lCrate.mrInputLockTimer > 0.0)
            return false;
        ItemBase oneitem = ItemBaseUtil.NewInstance(carriedItem).SetAmount(1);
        if (lCrate.mMode == MassStorageCrate.CrateMode.SingleStack)
        {
            if (lCrate.mItem != null)
            {
                bool canFit = false;
                if (!lCrate.ContainsSingleStackItem(oneitem, out canFit) || !canFit)
                    return false;
            }
            else if (carriedItem.mType != ItemType.ItemCubeStack && carriedItem.mType != ItemType.ItemStack && !lCrate.SwitchMode(MassStorageCrate.CrateMode.Items))
                return false;
        }
        else if (lCrate.mnLocalUsedStorage == 0)
        {
            if ((carriedItem.mType == ItemType.ItemCubeStack || carriedItem.mType == ItemType.ItemStack) && !lCrate.SwitchMode(MassStorageCrate.CrateMode.SingleStack))
                return false;
        }
        else if (carriedItem.mType == ItemType.ItemCubeStack || carriedItem.mType == ItemType.ItemStack)
            return false;
        else if (lCrate.mMode == MassStorageCrate.CrateMode.Items && lCrate.mnLocalUsedStorage >= lCrate.STORAGE_CRATE_SIZE)
            return false;
        this.mTargetCrate = lCrate;
        this.mTargetCrate.mrInputLockTimer = 5f;
        this.mrDroneTargetHeight = (float)((double)this.mTargetCrate.mnRequestedStackHeight - 1.0 + 0.25);
        this.mrCarryTimer = -1f;
        this.SetNewState(MSStockpiler.eState.SendingDrone);
        return true;
    }

    public bool HandleItemAdd()
    {
        int amount = this.mTargetCrate.mnLocalFreeStorage;
        ItemBase item = this.DroneInventory.RemoveAnySingle(amount);
        if (item != null && !this.mTargetCrate.AddItem(item))
            this.DroneInventory.AddItem(item);
        return true;
    }

    //Old single layer storage code
    //private bool AssignTargetCrateIfFree(MassStorageCrate lCrate)
    //{
    //    if (lCrate.mnLocalFreeStorage == 0 || (double)lCrate.mrInputLockTimer > 0.0)
    //        return false;
    //    this.mTargetCrate = lCrate;
    //    this.mTargetCrate.mrInputLockTimer = 5f;
    //    this.mrCarryTimer = -1f;
    //    this.SetNewState(MSStockpiler.eState.SendingDrone);
    //    return true;
    //}

    private void LookForAttachedStorage()
    {
        for (int index = 0; index < 4; ++index)
        {
            long x = this.mnX;
            long y = this.mnY;
            long z = this.mnZ;
            if (index == 0)
                --x;
            if (index == 1)
                ++x;
            if (index == 2)
                --z;
            if (index == 3)
                ++z;
            Segment segment = this.AttemptGetSegment(x, y, z);
            if (segment == null)
            {
                segment = WorldScript.instance.GetSegment(x, y, z);
                if (segment == null)
                {
                    Debug.Log((object)"LookForAttachedStorage did not find segment");
                    continue;
                }
            }
            if ((int)segment.GetCube(x, y, z) == 527)
            {
                MassStorageCrate massStorageCrate = segment.FetchEntity(eSegmentEntity.MassStorageCrate, x, y, z) as MassStorageCrate;
                if (massStorageCrate != null && (massStorageCrate.mbIsCenter || massStorageCrate.GetCenter() != null))
                {
                    this.mAttachedCrate = massStorageCrate.GetCenter();
                    this.SetNewState(MSStockpiler.eState.Idling);
                    break;
                }
            }
        }
    }

    private void CountUpStockpile()
    {
        //Loop over all crates and count up quantity of stockpiled item
        MassStorageCrate massStorageCrate = mAttachedCrate;
        ItemBase pickeditem = null;
        int foundcount = 0;
        bool singlestack = false;

        if (massStorageCrate != null)
        {
            for (int index = 0; index < massStorageCrate.mConnectedCrates.Count + 1; ++index)
            {
                for (int index2 = 0; index2 < massStorageCrate.STORAGE_CRATE_SIZE; ++index2)
                {
                    if (index == massStorageCrate.mConnectedCrates.Count) //Center crate!
                    {
                        if (massStorageCrate.mMode == MassStorageCrate.CrateMode.SingleStack)
                        {
                            pickeditem = massStorageCrate.mItem;
                            singlestack = true;
                        }
                        else
                            pickeditem = (massStorageCrate.mItems[index2]);
                    }
                    else
                    {
                        if (massStorageCrate.mConnectedCrates[index].mMode == MassStorageCrate.CrateMode.SingleStack)
                        {
                            pickeditem = massStorageCrate.mConnectedCrates[index].mItem;
                            singlestack = true;
                        }
                        else
                            pickeditem = massStorageCrate.mConnectedCrates[index].mItems[index2];
                    }

                    if (pickeditem != null)
                    {
                        if (this.ItemToStock.mType == ItemType.ItemCubeStack && pickeditem.mType == ItemType.ItemCubeStack)
                        {
                        }
                        if (pickeditem.Compare(this.ItemToStock))
                        {
                            foundcount += pickeditem.GetAmount();
                        }
                        pickeditem = null;
                    }
                    if (singlestack)
                    {
                        singlestack = false;
                        break;
                    }
                }
            }
            this.CurrentStock = foundcount;
        }
    }

    //Ancient code pre-community utils
    //private void CountUpStockpile()
    //{
    //    //Loop over all crates and count up quantity of stockpiled item
    //    MassStorageCrate massStorageCrate = mAttachedCrate.GetCenter();
    //    ItemBase pickeditem = null;
    //    int foundcount = 0;
    //    bool SearchByCube = false;
    //    int idcheck = 0;
    //    ushort valcheck = 0;

    //    if (this.ItemToStock.mType == ItemType.ItemCubeStack)
    //    {
    //        SearchByCube = true;
    //        ItemCubeStack stockeditem = (this.ItemToStock as ItemCubeStack);
    //        idcheck = (int)stockeditem.mCubeType;
    //        valcheck = stockeditem.mCubeValue;
    //    }
    //    else
    //        idcheck = this.ItemToStock.mnItemID;

    //    if (massStorageCrate != null)
    //    {
    //        for (int index = 0; index < massStorageCrate.mConnectedCrates.Count + 1; ++index)
    //        {
    //            for (int index2 = 0; index2 < massStorageCrate.STORAGE_CRATE_SIZE; ++index2)
    //            {
    //                if (index == massStorageCrate.mConnectedCrates.Count) //Center crate!
    //                {
    //                    if (massStorageCrate.mItems[index2] != null)
    //                        pickeditem = (massStorageCrate.mItems[index2]);
    //                }
    //                else if (massStorageCrate.mConnectedCrates[index].mItems[index2] != null)
    //                {
    //                    pickeditem = (massStorageCrate.mConnectedCrates[index].mItems[index2]);
    //                }

    //                if (pickeditem != null)
    //                {
    //                    if (SearchByCube && pickeditem.mType == ItemType.ItemCubeStack)
    //                    {
    //                        ItemCubeStack pickedcubestack = (pickeditem as ItemCubeStack);
    //                        if ((int)pickedcubestack.mCubeType == idcheck && pickedcubestack.mCubeValue == valcheck)
    //                            foundcount++;
    //                    }
    //                    else if (pickeditem.mnItemID == idcheck)
    //                    {
    //                        foundcount++;
    //                    }

    //                    pickeditem = null;
    //                }
    //            }
    //        }
    //    }
    //    this.CurrentStock = foundcount;
    //}

    private void MapCrateDistances()
    {
        List<KeyValuePair<float, int>> DistanceMap = new List<KeyValuePair<float, int>>();
        if (mAttachedCrate == null)
            return;
        MassStorageCrate lCrate = mAttachedCrate;
        this.IndexedDistances = new int[lCrate.mConnectedCrates.Count + 1];

        for (int index = 0; index < lCrate.mConnectedCrates.Count + 1; ++index)
        {
            if (index == lCrate.mConnectedCrates.Count) //Center crate!
            {
                if (lCrate != null)
                    DistanceMap.Add(new KeyValuePair<float, int>(new Vector3(this.mnX - lCrate.mnX, this.mnY - lCrate.mnY, this.mnZ - lCrate.mnZ).sqrMagnitude, index));
            }
            else if (lCrate.mConnectedCrates[index] != null)
            {
                DistanceMap.Add(new KeyValuePair<float, int>(new Vector3(this.mnX - lCrate.mConnectedCrates[index].mnX, this.mnY - lCrate.mConnectedCrates[index].mnY, this.mnZ - lCrate.mConnectedCrates[index].mnZ).sqrMagnitude, index));
            }
        }
        DistanceMap = DistanceMap.OrderBy(x => x.Key).ToList();
        for (int i = 0; i < DistanceMap.Count; i++)
        {
            this.IndexedDistances[i] = DistanceMap[i].Value;
        }
    }

    private bool HandleCrateLockOut()
    {
        CrateLockTimer -= LowFrequencyThread.mrPreviousUpdateTimeStep;
        if (this.LocalCrateLockOut && !CrateDistanceLockOut.CheckLock)
        {
            Debug.LogWarning("Local crate distance scan locked out but global lock isn't?");
            this.LocalCrateLockOut = false;
            return true;
        }
        //We're holding the global lock and our timer has expired
        if (CrateLockTimer < 0 && this.LocalCrateLockOut && CrateDistanceLockOut.CheckLock)
        {
            this.LocalCrateLockOut = false;
            CrateDistanceLockOut.CheckLock = false;
            return true;
        }
        //Still awaiting lockout time
        if (this.CrateLockTimer > 0 || this.LocalCrateLockOut || CrateDistanceLockOut.CheckLock)
            return true;
        //Lockout is free so claim it and scan the storage for crate distances
        else
        {
            this.LocalCrateLockOut = true;
            CrateDistanceLockOut.CheckLock = true;
            this.MapCrateDistances();
            this.CrateLockTimer = 0.3f;
            return false;
        }
    }

    private void SetNewState(MSStockpiler.eState lNewState)
    {
        if (this.meState != lNewState)
            this.RequestImmediateNetworkUpdate();
        this.meState = lNewState;
        this.mrStateTimer = 0.0f;
    }

    public override void DropGameObject()
    {
        base.DropGameObject();
        this.mbLinkedToGO = false;
    }

    public override void UnitySuspended()
    {
        this.CarryDrone = (GameObject)null;
        if ((Object)this.mCarriedObjectItem != (Object)null)
            UnityEngine.Object.Destroy((Object)this.mCarriedObjectItem);
        this.mCarriedObjectItem = (GameObject)null;
        this.CarryDroneClamp = (GameObject)null;
    }

    public override void UnityUpdate()
    {
        UIUtil.DisconnectUI(this);
        if (!this.mbLinkedToGO)
        {
            if (this.mWrapper == null || this.mWrapper.mGameObjectList == null)
                return;
            if ((Object)this.mWrapper.mGameObjectList[0].gameObject == (Object)null)
                Debug.LogError((object)"MSIP missing game object #0 (GO)?");
            this.CarryDrone = Extensions.Search(this.mWrapper.mGameObjectList[0].gameObject.transform, "CarryDrone").gameObject;
            MaterialPropertyBlock properties = new MaterialPropertyBlock();
            Color color = Color.white;
            if ((int)this.Tier == 1)
                color = Color.green;
            if ((int)this.Tier == 2)
                color = Color.cyan;
            if ((int)this.Tier == 3)
                color = Color.magenta;
            properties.SetColor("_GlowColor", color);
            this.CarryDrone.GetComponent<Renderer>().SetPropertyBlock(properties);
            this.CarryDroneClamp = Extensions.Search(this.mWrapper.mGameObjectList[0].gameObject.transform, "ClampPoint").gameObject;
            this.DigiTransmit = Extensions.Search(this.CarryDrone.transform, "DigiTransmit").gameObject;
            this.InputHopperObject = Extensions.Search(this.mWrapper.mGameObjectList[0].gameObject.transform, "Input Hopper").gameObject;
            this.mUnityDroneRestPos = this.CarryDrone.transform.position;
            this.mUnityDronePos = this.CarryDrone.transform.position;
            this.mbCarriedCubeNeedsConfiguring = true;
            this.mbLinkedToGO = true;
            this.meUnityState = this.meState;
        }
        else
        {
            if (this.meUnityState != this.meState)
            {
                if (this.meUnityState == MSStockpiler.eState.SendingDrone && this.meState == MSStockpiler.eState.RetrievingDrone)
                {
                    this.DigiTransmit.SetActive(true);
                    this.DigiTransmit.transform.localScale = new Vector3(0.5f, 0.5f, 1f);
                }
                this.meUnityState = this.meState;
            }
            if (this.meState == MSStockpiler.eState.Idling || this.meState == MSStockpiler.eState.SearchingForStorage || this.meState == MSStockpiler.eState.AwaitingForLowStock)
            {
                Vector3 vector3 = this.mForwards;
                vector3.x += 0.1f;
                vector3.Normalize();
                this.CarryDrone.transform.forward += (vector3 - this.CarryDrone.transform.forward) * Time.deltaTime * 0.5f;

                //My stuff for drifting back home
                Vector3 vectorhome = this.mUnityDroneRestPos - this.mUnityDronePos;
                float magnitude2 = vectorhome.magnitude;
                vectorhome /= magnitude2;
                if ((double)magnitude2 < 0.100000001490116)
                {
                    this.mUnityDronePos = this.mUnityDroneRestPos;
                }
                else
                {
                    this.CarryDrone.transform.position += vectorhome * Time.deltaTime * 0.5f;
                }
            }
            if (this.meState == MSStockpiler.eState.SendingDrone)
            {
                if (this.mUnityDroneTarget == Vector3.zero)
                    return;
                Vector3 vector3_1 = this.mUnityDroneTarget - this.mUnityDronePos;
                float magnitude = vector3_1.magnitude;
                vector3_1 /= magnitude;
                if ((double)magnitude < 0.100000001490116)
                {
                    this.mrDroneHeight -= Time.deltaTime;
                    this.mrCarryTimer = this.mrDroneHeight >= (this.mTargetCrate.mnLocalUsedStorage / 25) - 0.75 ? 1f : 0.0f;
                }
                else
                {
                    this.mrCarryTimer = 1f;
                    if ((double)this.mrDroneHeight < (double)this.mrDroneTargetHeight)
                    {
                        this.mrDroneHeight += Time.deltaTime;
                        this.CarryDrone.transform.Rotate(0.0f, 1f, 0.0f);
                    }
                    else
                        this.mUnityDronePos += vector3_1 * Time.deltaTime;
                }
                this.UpdateDronePos(this.mUnityDronePos);
                Vector3 vector3_2 = vector3_1 - this.CarryDrone.transform.forward;
                if (vector3_1 != Vector3.zero)
                {
                    vector3_2.y = 0.0f;
                    this.CarryDrone.transform.forward += vector3_2 * Time.deltaTime * 2.5f;
                }
            }
            if (this.mbCarriedCubeNeedsConfiguring)
            {
                this.mbCarriedCubeNeedsConfiguring = false;
                if ((Object)this.mCarriedObjectItem != (Object)null)
                    UnityEngine.Object.Destroy((Object)this.mCarriedObjectItem);
                if (!this.DroneInventory.IsEmpty() && this.DroneInventory.Inventory[0].mType != ItemType.ItemCubeStack)
                {
                    int index = (int)ItemEntry.mEntries[this.DroneInventory.Inventory[0].mnItemID].Object;
                    this.mCarriedObjectItem = (GameObject)Object.Instantiate((Object)SpawnableObjectManagerScript.instance.maSpawnableObjects[index], this.CarryDroneClamp.transform.position, this.CarryDroneClamp.transform.rotation);
                    this.mCarriedObjectItem.transform.parent = this.CarryDroneClamp.transform;
                    this.mCarriedObjectItem.transform.localPosition = Vector3.zero;
                    this.mCarriedObjectItem.transform.rotation = Quaternion.identity;
                    this.mCarriedObjectItem.SetActive(true);
                }
            }
            if (this.meState == MSStockpiler.eState.RetrievingDrone || this.meState == MSStockpiler.eState.AwaitingForLowStock || (this.meState == MSStockpiler.eState.Idling || this.meState == MSStockpiler.eState.LookingForAttachedStorage))
            {
                Vector3 vector3_1 = this.mUnityDroneRestPos - this.mUnityDronePos;
                float magnitude = vector3_1.magnitude;
                vector3_1 /= magnitude;
                if ((double)magnitude < 0.100000001490116)
                {
                    this.mrDroneHeight -= Time.deltaTime;
                    this.mUnityDronePos = this.mUnityDroneRestPos;
                    if ((double)this.mrDroneHeight <= 0.0)
                    {
                        this.mrCarryTimer = 0.0f;
                        this.mrDroneHeight = 0.0f;
                    }
                    else
                        this.mrCarryTimer = 1f;
                }
                else
                {
                    this.mrCarryTimer = 1f;
                    if ((double)this.mrDroneHeight < (double)this.mrDroneTargetHeight)
                    {
                        this.mrDroneHeight += Time.deltaTime;
                        this.CarryDrone.transform.Rotate(0.0f, 1f, 0.0f);
                    }
                    else
                        this.mUnityDronePos += vector3_1 * Time.deltaTime;
                }
                this.UpdateDronePos(this.mUnityDronePos);
                Vector3 vector3_2 = vector3_1 - this.CarryDrone.transform.forward;
                if (vector3_1 != Vector3.zero)
                {
                    vector3_2.y = 0.0f;
                    this.CarryDrone.transform.forward += vector3_2 * Time.deltaTime * 2.5f;
                }
            }

            if (this.mbWellBehindPlayer || this.mSegment.mbOutOfView)
            {
                if (this.InputHopperObject.activeSelf)
                    this.InputHopperObject.SetActive(false);
            }
            else if (!this.InputHopperObject.activeSelf)
                this.InputHopperObject.SetActive(true);
            if (this.mDroneSegment != null)
            {
                if (this.mDroneSegment.mbOutOfView)
                {
                    if (!this.CarryDrone.activeSelf)
                        return;
                    this.CarryDrone.SetActive(false);
                }
                else
                {
                    if (this.CarryDrone.activeSelf)
                        return;
                    this.CarryDrone.SetActive(true);
                }
            }
            else
            {
                if (!this.CarryDrone.activeSelf)
                    return;
                this.CarryDrone.SetActive(false);
            }
        }
    }

    private void UpdateDronePos(Vector3 lPos)
    {
        this.CarryDrone.transform.position += (lPos + new Vector3(0.0f, this.mrDroneHeight, 0.0f) - this.CarryDrone.transform.position) * Time.deltaTime;
    }

    private ItemBase GetCurrentHotBarItemOrCubeAsItem(out int lnAvailable, bool lastStackCount)
    {
        if ((Object)SurvivalHotBarManager.instance == (Object)null)
        {
            Debug.LogWarning((object)"SurvivalHotBarManager.instance is null??");
            lnAvailable = 0;
            return (ItemBase)null;
        }
        SurvivalHotBarManager.HotBarEntry currentHotBarEntry = SurvivalHotBarManager.instance.GetCurrentHotBarEntry();
        if (currentHotBarEntry == null)
        {
            lnAvailable = 0;
            return (ItemBase)null;
        }
        lnAvailable = !lastStackCount || currentHotBarEntry.lastStackCount <= 0 ? currentHotBarEntry.count : currentHotBarEntry.lastStackCount;
        if (currentHotBarEntry.state == SurvivalHotBarManager.HotBarEntryState.Empty)
            return (ItemBase)null;
        if ((int)currentHotBarEntry.cubeType != 0)
            return (ItemBase)ItemManager.SpawnCubeStack(currentHotBarEntry.cubeType, currentHotBarEntry.cubeValue, 1);
        if (currentHotBarEntry.itemType >= 0)
            return ItemManager.SpawnItem(currentHotBarEntry.itemType);
        Debug.LogError((object)"No cube and no item in hotbar?");
        return (ItemBase)null;
    }

    public bool TryDeliverItem(StorageUserInterface sourceEntity, ItemBase item, ushort cubeType, ushort cubeValue, bool sendImmediateNetworkUpdate)
    {
        ItemBase itemreceived = item == null ? (ItemBase)ItemManager.SpawnCubeStack(cubeType, cubeValue, 1) : item;
        if (this.LocalInventory.IsFull() || itemreceived.GetAmount() > this.LocalInventory.SpareCapacity() || (this.InventoryLock && this.CurrentStock >= this.QuantityToStock))
            return false;
        this.LocalInventory.AddItem(itemreceived);
        return true;        
    }

    public override bool ShouldNetworkUpdate()
    {
        return true;
    }

    public override void ReadNetworkUpdate(BinaryReader reader)
    {
        base.ReadNetworkUpdate(reader);
        MSStockpiler.eState eState = (MSStockpiler.eState)reader.ReadByte();
        switch (eState)
        {
            case MSStockpiler.eState.RetrievingDrone:
            case MSStockpiler.eState.Idling:
            case MSStockpiler.eState.LookingForAttachedStorage:
            case MSStockpiler.eState.SearchingForStorage:
                this.meState = eState;
                break;
        }
    }

    public override void WriteNetworkUpdate(BinaryWriter writer)
    {
        base.WriteNetworkUpdate(writer);
        writer.Write((byte)this.meState);
    }

    public override void OnDelete()
    {
        this.LocalInventory.DropOnDelete();
        this.DroneInventory.DropOnDelete();
        base.OnDelete();
    }

    public override bool ShouldSave()
    {
        return true;
    }

    public override int GetVersion()
    {
        return 2;   
    }

    public override void Write(BinaryWriter writer)
    {
        writer.Write(this.QuantityToStock);
        writer.Write(this.InventoryLock);
        if (this.ItemToStock != null)
            ItemFile.SerialiseItem(this.ItemToStock, writer);
        else
            ItemFile.SerialiseItem(null, writer);
        this.LocalInventory.WriteInventory(writer);
        this.DroneInventory.WriteInventory(writer);
    }

    public override void Read(BinaryReader reader, int entityVersion)
    {
        switch (entityVersion)
        {
            case 0:
                float num1 = (float)reader.ReadSingle();
                float num2 = (float)reader.ReadSingle();
                float num3 = (float)reader.ReadSingle();
                float num4 = (float)reader.ReadSingle();
                float num5 = (float)reader.ReadSingle();
                float num6 = (float)reader.ReadSingle();
                float num7 = (float)reader.ReadSingle();
                float num8 = (float)reader.ReadSingle();
                this.QuantityToStock = reader.ReadInt32();
                this.ItemToStock = ItemFile.DeserialiseItem(reader);
                ItemBase mCarriedItem = ItemFile.DeserialiseItem(reader);
                this.meState = MSStockpiler.eState.Unknown;
                //this.mbHoloPreviewDirty = true;
                if (mCarriedItem == null || mCarriedItem.mnItemID != -1 || mCarriedItem.mType == ItemType.ItemCubeStack)
                {
                    if (mCarriedItem != null)
                        this.DroneInventory.AddItem(mCarriedItem);
                    return;
                }
                Debug.LogWarning((object)"Error, saved MSOP had illegal item!");
                mCarriedItem = (ItemBase)null;
                break;
            case 1:
                float num01 = (float)reader.ReadSingle();
                float num02 = (float)reader.ReadSingle();
                float num03 = (float)reader.ReadSingle();
                float num04 = (float)reader.ReadSingle();
                float num05 = (float)reader.ReadSingle();
                float num06 = (float)reader.ReadSingle();
                float num07 = (float)reader.ReadSingle();
                float num08 = (float)reader.ReadSingle();
                this.QuantityToStock = reader.ReadInt32();
                this.ItemToStock = ItemFile.DeserialiseItem(reader);
                this.LocalInventory.ReadInventory(reader);
                this.DroneInventory.ReadInventory(reader);
                break;
            case 2:
                this.QuantityToStock = reader.ReadInt32();
                this.InventoryLock = reader.ReadBoolean();
                this.ItemToStock = ItemFile.DeserialiseItem(reader);
                this.LocalInventory.ReadInventory(reader);
                this.DroneInventory.ReadInventory(reader);
                break;
        } 
    }

    public override HoloMachineEntity CreateHolobaseEntity(Holobase holobase)
    {
        HolobaseEntityCreationParameters parameters = new HolobaseEntityCreationParameters((SegmentEntity)this);
        parameters.AddVisualisation(holobase.mPreviewCube).Color = Color.red;
        return holobase.CreateHolobaseEntity(parameters);
    }

    public enum eState
    {
        Unknown,
        LookingForAttachedStorage,
        Idling,
        SearchingForStorage,
        SendingDrone,
        RetrievingDrone,
        AwaitingForLowStock,
    }

    public string ItemConfigMachineName()
    {
        return "Set Item to Stock";
    }

    public void HandleItemSelected(ItemBase item)
    {
        MSStockpilerWindow.SetItemToStock(WorldScript.mLocalPlayer, this, item);
    }


}
public static class CrateDistanceLockOut
{
    public static bool CheckLock { get; set; }
}

