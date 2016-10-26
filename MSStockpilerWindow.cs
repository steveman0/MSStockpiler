using UnityEngine;
using FortressCraft.Community.Utilities;
using System.Collections.Generic;
using System;

public class MSStockpilerWindow : BaseMachineWindow
{
    public const string InterfaceName = "steveman0.MSStockpilerWindow";
    public const string InterfaceSetItemToStock = "SetItemToStock";
    public const string InterfaceSetStockpile = "SetStockpile";
    public const string InterfaceInventoryLock = "InventoryLock";

    public static bool dirty = false;
    public static bool networkredraw = false;
    private bool ItemSearch = false;
    private List<ItemBase> SearchResults;
    private int Counter;
    private string EntryString;


    public override void SpawnWindow(SegmentEntity targetEntity)
    {
        MSStockpiler port = targetEntity as MSStockpiler;

        if (port == null)
        {
            //GenericMachinePanelScript.instance.Hide();
            UIManager.RemoveUIRules("Machine");
            return;
        }
        //UIUtil.UIdelay = 0;
        //UIUtil.UILock = true;
        this.manager.SetTitle("Mass Storage Stockpiler");

        if (!ItemSearch)
        {
            int offset = 0;

            this.manager.AddIcon("stockitem", "empty", Color.white, 0, offset);
            this.manager.AddBigLabel("stocktitle", "Set Item to Stock", Color.white, 60, offset);
            this.manager.AddBigLabel("stocklimit", "0", Color.white, 260, offset);
            this.manager.AddButton("decreasestock", "Decrease Stock", 25, offset + 60);
            this.manager.AddButton("increasestock", "Increase Stock", 175, offset + 60);
            this.manager.AddBigLabel("locktitle", "Lock inventory above stock limit", Color.white, 10, offset + 120);
            this.manager.AddButton("locktoggle", "Toggle Lock", 25, offset + 170);
            this.manager.AddBigLabel("lockstatus", "UNKNOWN", Color.white, 175, offset + 170);
        }
        else
        {
            this.manager.AddButton("searchcancel", "Cancel", 100, 0);
            this.manager.AddBigLabel("searchtitle", "Enter Item Search Term", Color.white, 50, 40);
            this.manager.AddBigLabel("searchtext", "_", Color.cyan, 50, 65);
            if (this.SearchResults != null)
            {
                int count = this.SearchResults.Count;
                int spacing = 60; //Spacing between each registry line
                int yoffset = 100; //Offset below button row
                int labeloffset = 60; //x offset for label from icon

                for (int n = 0; n < count; n++)
                {
                    this.manager.AddIcon("itemicon" + n, "empty", Color.white, 0, yoffset + (spacing * n));
                    this.manager.AddBigLabel("iteminfo" + n, "Inventory Item", Color.white, labeloffset, yoffset + (spacing * n));
                }
            }
        }
        dirty = true;
    }

    public override void UpdateMachine(SegmentEntity targetEntity)
    {
        MSStockpiler port = targetEntity as MSStockpiler;

        if (port == null)
        {
            GenericMachinePanelScript.instance.Hide();
            UIManager.RemoveUIRules("Machine");
            return;
        }
        //UIUtil.UIdelay = 0;

        if (networkredraw)
        {
            networkredraw = false;
            this.manager.RedrawWindow();
        }
        if (!dirty)
            return;

        if (!ItemSearch)
        {

            ItemBase item = port.ItemToStock;
            int stocklimit = port.QuantityToStock;

            if (item != null)
            {
                string itemname = ItemManager.GetItemName(item);
                string iconname = ItemManager.GetItemIcon(item);

                this.manager.UpdateIcon("stockitem", iconname, Color.white);
                this.manager.UpdateLabel("stocktitle", itemname, Color.white);
                this.manager.UpdateLabel("stocklimit", stocklimit.ToString("N0"), Color.white);
                this.manager.UpdateLabel("lockstatus", port.InventoryLock ? "Locked" : "Unlocked", Color.white);
            }
        }
        else
        {
            if (this.SearchResults == null)
            {
                this.Counter++;
                foreach (char c in Input.inputString)
                {
                    if (c == "\b"[0])  //Backspace
                    {
                        if (this.EntryString.Length != 0)
                            this.EntryString = this.EntryString.Substring(0, this.EntryString.Length - 1);
                    }
                    else if (c == "\n"[0] || c == "\r"[0]) //Enter or Return
                    {
                        this.SearchResults = new List<ItemBase>();

                        for (int n = 0; n < ItemEntry.mEntries.Length; n++)
                        {
                            if (ItemEntry.mEntries[n] == null) continue;
                            if (ItemEntry.mEntries[n].Name.ToLower().Contains(this.EntryString.ToLower()))
                                this.SearchResults.Add(ItemManager.SpawnItem(ItemEntry.mEntries[n].ItemID));
                        }
                        for (int n = 0; n < TerrainData.mEntries.Length; n++)
                        {
                            bool foundvalue = false;
                            if (TerrainData.mEntries[n] == null) continue;
                            if (TerrainData.mEntries[n].Name.ToLower().Contains(this.EntryString.ToLower()))
                            {
                                int count = TerrainData.mEntries[n].Values.Count;
                                for (int m = 0; m < count; m++)
                                {
                                    if (TerrainData.mEntries[n].Values[m].Name.ToLower().Contains(this.EntryString.ToLower()))
                                    {
                                        this.SearchResults.Add(ItemManager.SpawnCubeStack(TerrainData.mEntries[n].CubeType, TerrainData.mEntries[n].Values[m].Value, 1));
                                        foundvalue = true;
                                    }
                                }
                                if (!foundvalue)
                                    this.SearchResults.Add(ItemManager.SpawnCubeStack(TerrainData.mEntries[n].CubeType, TerrainData.mEntries[n].DefaultValue, 1));
                            }
                            if ((this.EntryString.ToLower().Contains("component") || this.EntryString.ToLower().Contains("placement") || this.EntryString.ToLower().Contains("multi")) && TerrainData.mEntries[n].CubeType == 600)
                            {
                                int count = TerrainData.mEntries[n].Values.Count;
                                for (int m = 0; m < count; m++)
                                {
                                    this.SearchResults.Add(ItemManager.SpawnCubeStack(600, TerrainData.mEntries[n].Values[m].Value, 1));
                                }
                            }
                        }
                        if (this.SearchResults.Count == 0)
                            this.SearchResults = null;

                        UIManager.mbEditingTextField = false;
                        UIManager.RemoveUIRules("TextEntry");

                        this.manager.RedrawWindow();
                        return;
                    }
                    else
                        this.EntryString += c;
                }
                this.manager.UpdateLabel("searchtext", this.EntryString + (this.Counter % 20 > 10 ? "_" : ""), Color.cyan);
                dirty = true;
                return;
            }
            else
            {
                this.manager.UpdateLabel("searchtitle", "Searching for:", Color.white);
                this.manager.UpdateLabel("searchtext", this.EntryString, Color.cyan);
                int count = this.SearchResults.Count;
                for (int n = 0; n < count; n++)
                {
                    ItemBase item = this.SearchResults[n];
                    string itemname = ItemManager.GetItemName(item);
                    string iconname = ItemManager.GetItemIcon(item);

                    this.manager.UpdateIcon("itemicon" + n, iconname, Color.white);
                    this.manager.UpdateLabel("iteminfo" + n, itemname, Color.white);
                }
            }
        }
        dirty = false;
    }

    public override bool ButtonClicked(string name, SegmentEntity targetEntity)
    {
        MSStockpiler port = targetEntity as MSStockpiler;

        if (name == "stockitem")
        {
            if (port.ItemToStock != null)
            {
                MSStockpilerWindow.SetItemToStock(WorldScript.mLocalPlayer, port, null);
                this.manager.RedrawWindow();
            }
            return true;
        }
        else if (name == "searchcancel")
        {
            this.ItemSearch = false;
            this.SearchResults = null;
            UIManager.mbEditingTextField = false;
            UIManager.RemoveUIRules("TextEntry");
            this.EntryString = "";
            this.manager.RedrawWindow();
        }
        else if (name == "increasestock")
        {
            int amount = 100;
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                amount = 10;
            if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
                amount = 1;
            if (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt))
                amount = 1000;
            int stockpile = port.QuantityToStock + amount;
            port.SetStockpile(stockpile);
            if (!WorldScript.mbIsServer)
                NetworkManager.instance.SendInterfaceCommand(InterfaceName, InterfaceSetStockpile, stockpile.ToString(), (ItemBase)null, (SegmentEntity)port, 0.0f);
            dirty = true;
            return true;
        }
        else if (name == "decreasestock")
        {
            int amount = 100;
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                amount = 10;
            if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
                amount = 1;
            if (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt))
                amount = 1000;
            int stockpile = port.QuantityToStock - amount;
            port.SetStockpile(stockpile);
            if (!WorldScript.mbIsServer)
                NetworkManager.instance.SendInterfaceCommand(InterfaceName, InterfaceSetStockpile, stockpile.ToString(), (ItemBase)null, (SegmentEntity)port, 0.0f);
            dirty = true;
            return true;
        }
        else if (name == "locktoggle")
        {
            MSStockpilerWindow.InventoryLock(WorldScript.mLocalPlayer, port, !port.InventoryLock);
        }
        else if (name.Contains("itemicon"))
        {
            int slotNum = -1;
            int.TryParse(name.Replace("itemicon", ""), out slotNum); //Get slot name as number
            if (slotNum > -1)
            {
                MSStockpilerWindow.SetItemToStock(WorldScript.mLocalPlayer, port, this.SearchResults[slotNum]);
                this.SearchResults = null;
                this.ItemSearch = false;
                this.EntryString = "";
                this.manager.RedrawWindow();
            }
        }
        return false;
    }

    public override bool ButtonRightClicked(string name, SegmentEntity targetEntity)
    {
        if (name == "stockitem")
        {
            this.ItemSearch = true;
            UIManager.mbEditingTextField = true;
            UIManager.AddUIRules("TextEntry", UIRules.RestrictMovement | UIRules.RestrictLooking | UIRules.RestrictBuilding | UIRules.RestrictInteracting | UIRules.SetUIUpdateRate);
            this.Redraw(targetEntity);
            GenericMachinePanelScript.instance.Scroll_Bar.GetComponent<UIScrollBar>().scrollValue = 0.0f;
            return true;
        }
        else
            return base.ButtonRightClicked(name, targetEntity);
    }

    public override void HandleItemDrag(string name, ItemBase draggedItem, DragAndDropManager.DragRemoveItem dragDelegate, SegmentEntity targetEntity)
    {
        MSStockpiler port = targetEntity as MSStockpiler;

        if (name == "stockitem") // drag drop to a slot
        {
            if (this.manager.mWindowLookup[name + "_icon"].GetComponent<UISprite>().spriteName == "empty")
            {
                MSStockpilerWindow.SetItemToStock(WorldScript.mLocalPlayer, port, draggedItem);
                this.manager.RedrawWindow();
            }
        }
        return;
    }

    public override void OnClose(SegmentEntity targetEntity)
    {
        this.SearchResults = null;
        this.ItemSearch = false;
        this.EntryString = "";
        UIManager.mbEditingTextField = false;
        UIManager.RemoveUIRules("TextEntry");
        base.OnClose(targetEntity);
    }

    public static bool SetItemToStock(Player player, MSStockpiler port, ItemBase itemtostock)
    {
        port.SetItemToStock(itemtostock);
        if (player.mbIsLocalPlayer && itemtostock != null)
            FloatingCombatTextManager.instance.QueueText(port.mnX, port.mnY + 1L, port.mnZ, 1f, "Stocking: " + ItemManager.GetItemName(itemtostock), ItemColor(itemtostock), 1.5f);
        else
            FloatingCombatTextManager.instance.QueueText(port.mnX, port.mnY + 1L, port.mnZ, 1f, "Stockpile cleared!", Color.blue, 1.5f);
        port.MarkDirtyDelayed();
        networkredraw = true;
        if (!WorldScript.mbIsServer)
            NetworkManager.instance.SendInterfaceCommand(InterfaceName, InterfaceSetItemToStock, (string)null, itemtostock, (SegmentEntity)port, 0.0f);
        return true;
    }

    public static bool SetStockpile(Player player, MSStockpiler port, int stockpile)
    {
        port.SetStockpile(stockpile);
        port.MarkDirtyDelayed();
        dirty = true;
        if (!WorldScript.mbIsServer)
            NetworkManager.instance.SendInterfaceCommand(InterfaceName, InterfaceSetStockpile, stockpile.ToString(), (ItemBase)null, (SegmentEntity)port, 0.0f);
        return true;
    }

    public static bool InventoryLock(Player player, MSStockpiler port, bool locksetting)
    {
        port.InventoryLock = locksetting;
        port.MarkDirtyDelayed();
        dirty = true;
        if (!WorldScript.mbIsServer)
            NetworkManager.instance.SendInterfaceCommand(InterfaceName, InterfaceInventoryLock, locksetting ? "true" : "false", (ItemBase)null, (SegmentEntity)port, 0.0f);
        return true;
    }

    public static NetworkInterfaceResponse HandleNetworkCommand(Player player, NetworkInterfaceCommand nic)
    {
        MSStockpiler port = nic.target as MSStockpiler;
        string key = nic.command;
        if (key != null)
        {
            if (nic.command == InterfaceSetItemToStock)
            {
                MSStockpilerWindow.SetItemToStock(player, port, nic.itemContext);
            }
            else if (key == InterfaceSetStockpile)
            {
                int stockpile;
                if (int.TryParse(nic.payload ?? "0", out stockpile))
                    MSStockpilerWindow.SetStockpile(player, port, stockpile);
            }
            else if (key == InterfaceInventoryLock)
            {
                MSStockpilerWindow.InventoryLock(player, port, nic.payload == "true");
            }
        }
        return new NetworkInterfaceResponse()
        {
            entity = (SegmentEntity)port,
            inventory = player.mInventory
        };
    }

    private static Color ItemColor(ItemBase itemBase)
    {
        Color lCol = Color.green;
        if (itemBase.mType == ItemType.ItemCubeStack)
        {
            ItemCubeStack itemCubeStack = itemBase as ItemCubeStack;
            if (CubeHelper.IsGarbage(itemCubeStack.mCubeType))
                lCol = Color.red;
            if (CubeHelper.IsSmeltableOre(itemCubeStack.mCubeType))
                lCol = Color.green;
        }
        if (itemBase.mType == ItemType.ItemStack)
            lCol = Color.cyan;
        if (itemBase.mType == ItemType.ItemSingle)
            lCol = Color.white;
        if (itemBase.mType == ItemType.ItemCharge)
            lCol = Color.magenta;
        if (itemBase.mType == ItemType.ItemDurability)
            lCol = Color.yellow;
        if (itemBase.mType == ItemType.ItemLocation)
            lCol = Color.gray;

        return lCol;
    }
}

