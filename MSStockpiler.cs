using UnityEngine;
using System.IO;
using System.Collections.Generic;
using System.Linq;


public class MSStockpiler : MachineEntity
{
    private float mrDroneTargetHeight = 0.25f;
    public float mrMaxSearchDistance = 10f;
    public MSStockpiler.eState meState;
    private MassStorageCrate mAttachedCrate;
    private MassStorageCrate mTargetCrate;
    public ItemBase mCarriedItem;
    public float mrCarryTimer;
    public float mrCarryDistance;
    private float mrDroneHeight;
    private Vector3 mForwards;
    public float mrStateTimer;
    private bool mbLinkedToGO;
    private GameObject CarryDrone;
    private GameObject CarryDroneClamp;
    private GameObject mCarriedObjectItem;
    private bool mbCarriedCubeNeedsConfiguring;
    private Vector3 mUnityDronePos;
    private Vector3 mUnityDroneTarget;
    private Vector3 mUnityDroneRestPos;
    public int QuantityToStock;
    public ItemBase ItemToStock;
    private float PopupDebounce = 0.0f;
    public int CurrentStock = 0;
    private float StockRefresh = 0.0f;
    private bool LocalCrateLockOut = false;
    private float CrateLockTimer = 0.0f;
    public int[] IndexedDistances;

    public MSStockpiler(Segment segment, long x, long y, long z, ushort cube, byte flags, ushort lValue, bool fromDisk)
    : base(eSegmentEntity.MassStorageInputPort, SpawnableObjectEnum.MassStorageInputPort, x, y, z, cube, flags, lValue, Vector3.zero, segment)
  {
        this.meState = MSStockpiler.eState.Unknown;
        this.mbNeedsLowFrequencyUpdate = true;
        this.mbNeedsUnityUpdate = true;
        this.mForwards = SegmentCustomRenderer.GetRotationQuaternion(flags) * Vector3.forward;
        this.mForwards.Normalize();
        this.mrMaxSearchDistance = 1f;
    }

    public override string GetPopupText()
    {
        MSStockpiler port = (MSStockpiler)WorldScript.instance.localPlayerInstance.mPlayerBlockPicker.selectedEntity;
        string lstr1;
        lstr1 = "Mass Storage Stockpiler Port\n";
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

        //Hack code to give mCarriedItem
        if (Input.GetKey(KeyCode.RightAlt))
            this.mCarriedItem = itemtostock;

        if (this.mAttachedCrate == null)
        {
            lstr3 = "\nLooking for mass storage crate...";
        }
        else if (this.IndexedDistances == null)
        {
            lstr3 = "\nSurveying mass storage crate distances...";
        }
        else
        {
            if (itemtostock != null && itemtostock.mnItemID == -1 && itemtostock.mType != ItemType.ItemCubeStack)
            {
                Debug.LogWarning((object)("Error, ItemID was " + (object)itemtostock.mnItemID));
                itemtostock = (ItemBase)null;
            }
            //Check assigned Item for stocking and display status
            if (this.ItemToStock != null)
                lstr2 = !WorldScript.mLocalPlayer.mResearch.IsKnown(this.ItemToStock) ? "\nCurrently stocking : Unknown Material" : "\nCurrently stocking : " + ItemManager.GetItemName(this.ItemToStock);
            //Check current hotbar item and display ability to set stockpile target
            lstr3 = itemtostock != null ? (!WorldScript.mLocalPlayer.mResearch.IsKnown(itemtostock) ? lstr2 + "\nPress T to set Stockpile target to Unknown Material" : lstr2 + "\nPress T to set Stockpile target to " + ItemManager.GetItemName(itemtostock)) : (this.ItemToStock != null ? lstr2 + "\nPress T to clear Stockpile target" : lstr2 + "\nSelect an item in your Hotbar to set Stockpile target");
            if (Input.GetButton("Store") && MSStockpilerWindow.SetItemToStock(WorldScript.mLocalPlayer, this, itemtostock) && this.PopupDebounce < 0 && UIManager.AllowInteracting)
            {
                if (itemtostock == null)
                {
                    AudioHUDManager.instance.HUDOut();
                    Debug.Log((object)"Stockpiler Port itemtostock cleared");
                }
                else
                {
                    AudioHUDManager.instance.HUDIn();
                    Debug.LogWarning((object)("Set Stockpiler to " + ItemManager.GetItemName(itemtostock)));
                }
                this.PopupDebounce = 0.3f;
            }
            if (this.ItemToStock != null)
            {
                int amount = 0;
                lstr4 = "\nStockpile quantity: " + this.CurrentStock + "/" + this.QuantityToStock + "\nUse (E/Q) to change stockpile quantity by ";
                if (Input.GetKey(KeyCode.LeftShift))
                    amount = 10;
                else if (Input.GetKey(KeyCode.LeftControl))
                    amount = 1;
                else if (Input.GetKey(KeyCode.LeftAlt))
                    amount = 1000;
                else
                    amount = 100;
                lstr4 += amount + "\n(Try Ctrl, Shift, and Alt!)";
                if (this.PopupDebounce < 0 && Input.GetButton("Interact") && UIManager.AllowInteracting)
                {
                    MSStockpilerWindow.SetStockpile(WorldScript.mLocalPlayer, port, this.QuantityToStock + amount);
                    this.PopupDebounce = 0.3f;
                }
                if (this.PopupDebounce < 0 && Input.GetButton("Extract") && UIManager.AllowInteracting)
                {
                    MSStockpilerWindow.SetStockpile(WorldScript.mLocalPlayer, port, this.QuantityToStock - amount);
                    this.PopupDebounce = 0.3f;
                }
            }
            this.PopupDebounce -= Time.deltaTime;
        }
        string lstr5 = string.Concat(new object[3]
            {
                (object) lstr1,
                (object) lstr3,
                (object) lstr4
            });
        return (lstr5);
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
        else if (lItem.mType == ItemType.ItemCubeStack)
        {
            if (this.ItemToStock.mType == ItemType.ItemCubeStack)
            {
                if ((int)(lItem as ItemCubeStack).mCubeType == (int)(this.ItemToStock as ItemCubeStack).mCubeType)
                    return;
                //this.mbHoloPreviewDirty = true;
                Debug.Log((object)"MSOP Set itemtostock to different CubeStack type");
            }
            else
            {
                //this.mbHoloPreviewDirty = true;
                Debug.Log((object)"MSOP Set itemtostock to CubeStack type");
            }
        }
        else
        {
            if (lItem.mnItemID == this.ItemToStock.mnItemID)
                return;
            //this.mbHoloPreviewDirty = true;
            Debug.Log((object)("MSOP Set itemtostock to " + ItemManager.GetItemName(lItem.mnItemID)));
        }
        this.ItemToStock = lItem;
        MSStockpiler port = (MSStockpiler)WorldScript.instance.localPlayerInstance.mPlayerBlockPicker.selectedEntity;
        MSStockpilerWindow.SetStockpile(WorldScript.mLocalPlayer, port, 0);
        this.MarkDirtyDelayed();
    }

    public void SetStockpile(int stockpile)
    {
        this.QuantityToStock = stockpile;
        if (this.QuantityToStock < 0)
            this.QuantityToStock = 0;
    }

    private bool IsCrateCloseEnough(MassStorageCrate lCrate)
    {
        return (double)new Vector3((float)this.mnX - (float)lCrate.mnX, (float)this.mnY - (float)lCrate.mnY, (float)this.mnZ - (float)lCrate.mnZ).sqrMagnitude < (double)this.mrMaxSearchDistance * (double)this.mrMaxSearchDistance;
    }

    public override void LowFrequencyUpdate()
    {
        this.StockRefresh -= LowFrequencyThread.mrPreviousUpdateTimeStep;
        if (this.meState == MSStockpiler.eState.Unknown)
            this.SetNewState(MSStockpiler.eState.LookingForAttachedStorage);
        if (this.meState == MSStockpiler.eState.LookingForAttachedStorage)
            this.LookForAttachedStorage();
        if (this.mAttachedCrate == null)
        {
            this.SetNewState(MSStockpiler.eState.LookingForAttachedStorage);
        }
        else
        {
            if (this.meState == MSStockpiler.eState.SendingDrone)
            {
                if (this.mCarriedItem == null)
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
                    this.mTargetCrate.AddItem(this.mCarriedItem);
                    this.mTargetCrate.mrInputLockTimer = 0.0f;
                    this.mTargetCrate = (MassStorageCrate)null;
                    this.SetNewState(MSStockpiler.eState.RetrievingDrone);
                    this.mrCarryTimer = 1f;
                    this.mCarriedItem = (ItemBase)null;
                    this.mrCarryTimer = this.mrCarryDistance;
                    this.mbCarriedCubeNeedsConfiguring = true;
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
            else if (IndexedDistances.Count() != mAttachedCrate.GetCenter().mConnectedCrates.Count + 1 || LocalCrateLockOut)
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
                    this.meState = MSStockpiler.eState.Idling;
            }

            if (this.meState == MSStockpiler.eState.Idling)
            {
                if (this.mCarriedItem != null)
                {
                    this.meState = MSStockpiler.eState.SearchingForStorage;
                    this.mbCarriedCubeNeedsConfiguring = true;
                }
                this.mrStateTimer += LowFrequencyThread.mrPreviousUpdateTimeStep;
            }
            if (this.meState == MSStockpiler.eState.SearchingForStorage)
            {
                if (this.mCarriedItem == null)
                {
                    Debug.LogWarning((object)"Error, MSIP SearchingForStorage but not carrying item?!");
                    this.meState = MSStockpiler.eState.Idling;
                    return;
                }
                if (this.mAttachedCrate.GetCenter() == null)
                    return;
                //Carried item is the Item to stock, don't overfill
                if (ItemOrCubeCompare(this.mCarriedItem, this. ItemToStock))
                {
                    if (this.CurrentStock >= this.QuantityToStock)
                    {
                        this.meState = MSStockpiler.eState.AwaitingForLowStock;
                        return;
                    }
                }
                int count = this.mAttachedCrate.GetCenter().mConnectedCrates.Count;
                int crateindex;
                for (int index = 0; index < count + 1; ++index)
                {
                    try
                    {
                        crateindex = IndexedDistances[index];
                    }
                    catch (System.IndexOutOfRangeException)
                    {
                        this.meState = MSStockpiler.eState.Idling;
                        IndexedDistances = null;
                        return;
                    }
                    if (crateindex == count + 1)
                    {
                        if (this.AssignTargetCrateIfFree(this.mAttachedCrate.GetCenter()))
                            return;
                    } 
                    else
                    {
                        if (this.AssignTargetCrateIfFree(this.mAttachedCrate.GetCenter().mConnectedCrates[crateindex]))
                            return;
                    }
                            
                }
                this.mrStateTimer += LowFrequencyThread.mrPreviousUpdateTimeStep;
            }
            
        }
    }

    private bool AssignTargetCrateIfFree(MassStorageCrate lCrate)
    {
        if (lCrate.LocalFreeStorage == 0 || (double)lCrate.mrInputLockTimer > 0.0)
            return false;
        this.mTargetCrate = lCrate;
        this.mTargetCrate.mrInputLockTimer = 5f;
        this.mrCarryTimer = -1f;
        this.SetNewState(MSStockpiler.eState.SendingDrone);
        return true;
    }

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
                    this.mAttachedCrate = massStorageCrate;
                    this.SetNewState(MSStockpiler.eState.Idling);
                    break;
                }
            }
        }
    }

    private void CountUpStockpile()
    {
        //Loop over all crates and count up quantity of stockpiled item
        MassStorageCrate massStorageCrate = mAttachedCrate.GetCenter();
        ItemBase pickeditem = null;
        int foundcount = 0;
        bool SearchByCube = false;
        int idcheck = 0;
        
        if (this.ItemToStock.mType == ItemType.ItemCubeStack)
        {
            SearchByCube = true;
            idcheck = (int)(this.ItemToStock as ItemCubeStack).mCubeType;
        }
        else
            idcheck = this.ItemToStock.mnItemID;

        if (massStorageCrate != null)
        {
            for (int index = 0; index < massStorageCrate.mConnectedCrates.Count + 1; ++index)
            {
                for (int index2 = 0; index2 < massStorageCrate.STORAGE_CRATE_SIZE; ++index2)
                {
                    if (index == massStorageCrate.mConnectedCrates.Count) //Center crate!
                    {
                        if (massStorageCrate.mItems[index2] != null)
                            pickeditem = (massStorageCrate.mItems[index2]);
                    }
                    else if (massStorageCrate.mConnectedCrates[index].mItems[index2] != null)
                    {
                        pickeditem = (massStorageCrate.mConnectedCrates[index].mItems[index2]);
                    }
                    
                    if (pickeditem != null)
                    {
                        if (SearchByCube && pickeditem.mType == ItemType.ItemCubeStack)
                        {
                            //Debug.Log("in loop item: " + pickeditem + " " + (pickeditem as ItemCubeStack).mCubeType);
                            if ((int)(pickeditem as ItemCubeStack).mCubeType == idcheck)
                                foundcount++;
                        }
                        else if (pickeditem.mnItemID == idcheck)
                        {
                            foundcount++;
                           // Debug.Log("in loop item: " + pickeditem + " " + pickeditem.mnItemID + " " + (pickeditem as ItemCubeStack).mCubeType);
                        }
                            
                        pickeditem = null;
                    }
                }
            }
        }
        this.CurrentStock = foundcount;
    }

    private void MapCrateDistances()
    {
        List<KeyValuePair<float, int>> DistanceMap = new List<KeyValuePair<float, int>>();
        if (mAttachedCrate.GetCenter() == null)
            return;
        MassStorageCrate lCrate = mAttachedCrate.GetCenter();
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
        if (!this.mbLinkedToGO)
        {
            if (this.mWrapper == null || this.mWrapper.mGameObjectList == null)
                return;
            if ((Object)this.mWrapper.mGameObjectList[0].gameObject == (Object)null)
                Debug.LogError((object)"MSIP missing game object #0 (GO)?");
            this.CarryDrone = Extensions.Search(this.mWrapper.mGameObjectList[0].gameObject.transform, "CarryDrone").gameObject;
            this.CarryDroneClamp = Extensions.Search(this.mWrapper.mGameObjectList[0].gameObject.transform, "ClampPoint").gameObject;
            this.mUnityDroneRestPos = this.CarryDrone.transform.position;
            this.mUnityDronePos = this.CarryDrone.transform.position;
            this.mbCarriedCubeNeedsConfiguring = true;
            this.mbLinkedToGO = true;
        }
        else
        {
            if (this.meState == MSStockpiler.eState.Idling || this.meState == MSStockpiler.eState.SearchingForStorage)
            {
                Vector3 vector3 = this.mForwards;
                vector3.x += 0.1f;
                vector3.Normalize();
                this.CarryDrone.transform.forward += (vector3 - this.CarryDrone.transform.forward) * Time.deltaTime * 0.5f;
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
                    this.mrCarryTimer = (double)this.mrDroneHeight >= -0.75 ? 1f : 0.0f;
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
                this.CarryDrone.transform.position = this.mUnityDronePos + new Vector3(0.0f, this.mrDroneHeight, 0.0f);
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
                if (this.mCarriedItem != null && this.mCarriedItem.mType != ItemType.ItemCubeStack)
                {
                    int index = (int)ItemEntry.mEntries[this.mCarriedItem.mnItemID].Object;
                    this.mCarriedObjectItem = (GameObject)Object.Instantiate((Object)SpawnableObjectManagerScript.instance.maSpawnableObjects[index], this.CarryDroneClamp.transform.position, this.CarryDroneClamp.transform.rotation);
                    this.mCarriedObjectItem.transform.parent = this.CarryDroneClamp.transform;
                    this.mCarriedObjectItem.transform.localPosition = Vector3.zero;
                    this.mCarriedObjectItem.transform.rotation = Quaternion.identity;
                    this.mCarriedObjectItem.SetActive(true);
                }
            }
            if (this.meState != MSStockpiler.eState.RetrievingDrone)
                return;
            Vector3 vector3_3 = this.mUnityDroneRestPos - this.mUnityDronePos;
            float magnitude1 = vector3_3.magnitude;
            vector3_3 /= magnitude1;
            if ((double)magnitude1 < 0.100000001490116)
            {
                this.mrDroneHeight -= Time.deltaTime;
                this.mUnityDronePos = this.mUnityDroneRestPos;
                this.mrCarryTimer = (double)this.mrDroneHeight >= 0.0 ? 1f : 0.0f;
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
                    this.mUnityDronePos += vector3_3 * Time.deltaTime;
            }
            this.CarryDrone.transform.position = this.mUnityDronePos + new Vector3(0.0f, this.mrDroneHeight, 0.0f);
            Vector3 vector3_4 = vector3_3 - this.CarryDrone.transform.forward;
            if (!(vector3_3 != Vector3.zero))
                return;
            vector3_4.y = 0.0f;
            this.CarryDrone.transform.forward += vector3_4 * Time.deltaTime * 2.5f;
        }
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

    public bool ItemOrCubeCompare(ItemBase item1, ItemBase item2)
    {
        if (item1.mType == ItemType.ItemCubeStack && item2.mType == ItemType.ItemCubeStack)
        {
            if ((int)(item1 as ItemCubeStack).mCubeType == (int)(item2 as ItemCubeStack).mCubeType)
                return true;
        }
        else if (item1.mnItemID == item2.mnItemID)
            return true;
        return false;
    }

    public override bool ShouldSave()
    {
        return true;
    }

    public override void Write(BinaryWriter writer)
    {
        float num = 0.0f;
        writer.Write(num);
        writer.Write(num);
        writer.Write(num);
        writer.Write(num);
        writer.Write(num);
        writer.Write(num);
        writer.Write(num);
        writer.Write(num);
        writer.Write(this.QuantityToStock);
        ItemFile.SerialiseItem(this.ItemToStock, writer);
        ItemFile.SerialiseItem(this.mCarriedItem, writer);
    }

    public override void Read(BinaryReader reader, int entityVersion)
    {
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
        this.mCarriedItem = ItemFile.DeserialiseItem(reader);
        this.meState = MSStockpiler.eState.Unknown;
        //this.mbHoloPreviewDirty = true;
        if (this.mCarriedItem == null || this.mCarriedItem.mnItemID != -1 || this.mCarriedItem.mType == ItemType.ItemCubeStack)
            return;
        Debug.LogWarning((object)"Error, saved MSOP had illegal item!");
        this.mCarriedItem = (ItemBase)null;
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
}
public static class CrateDistanceLockOut
{
    public static bool CheckLock { get; set; }
}

