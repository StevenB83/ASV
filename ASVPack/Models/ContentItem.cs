﻿using SavegameToolkit;
using SavegameToolkit.Propertys;
using SavegameToolkit.Structs;
using SavegameToolkit.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace ASVPack.Models
{
    [DataContract]
    public class ContentItem
    {
        [DataMember] public string ClassName { get; set; } = "";
        [DataMember] public string CustomName { get; set; } = "";
        [DataMember] public string CraftedByPlayer { get; set; } = "";
        [DataMember] public long OwnerPlayerId { get; set; } = 0;

        [DataMember] public string CraftedByTribe { get; set; } = "";
        [DataMember] public int Quantity { get; set; } = 1;
        [DataMember] public bool IsBlueprint { get; set; } = false;
        [DataMember] public bool IsEngram { get; set; } = false;
        [DataMember] public float? Rating { get; set; } = null;
        [DataMember] public DateTime? UploadedTime { get; set; } = null;
        public double UploadedTimeInGame { get; set; } = 0;


        public ContentItem(StructPropertyList uploadData)
        {
            var testRef = uploadData.Properties[0];
            ClassName = (((PropertyObject)testRef).Value as ObjectReference)?.ObjectString?.Name??"";
            if(ClassName.Length > 0) ClassName = ClassName.Substring(ClassName.LastIndexOf(".")+1);

            OwnerPlayerId = (long)uploadData.GetPropertyValue<UInt64>("OwnerPlayerDataID");

            CustomName = uploadData.GetPropertyValue<string>("CustomItemName");
            IsBlueprint = uploadData.GetPropertyValue<bool>("bIsBlueprint");
            IsEngram = uploadData.GetPropertyValue<bool>("bIsEngram");
            Quantity = uploadData.GetPropertyValue<int>("ItemQuantity", 0, 1);

            if (Quantity == 0) Quantity = 1;

            CraftedByTribe = uploadData.GetPropertyValue<string>("CrafterTribeName");
            CraftedByPlayer = uploadData.GetPropertyValue<string>("CrafterCharacterName");            
            UploadedTimeInGame = uploadData.GetPropertyValue<double>("CreationTime");
            if (uploadData.HasAnyProperty("ItemRating"))
            {
                var ratingProp = uploadData.GetTypedProperty<PropertyFloat>("ItemRating")?.Value;
                if (!float.IsNaN(ratingProp.Value))
                {
                    Rating = ratingProp.Value;
                }
                else
                {
                    Rating = 0.0001f;
                }
            }

        }

        public ContentItem(GameObject itemObject)
        {
            ClassName = itemObject.ClassString;
            CustomName = itemObject.GetPropertyValue<string>("CustomName");
            IsBlueprint = itemObject.GetPropertyValue<bool>("bIsBlueprint");
            IsEngram = itemObject.GetPropertyValue<bool>("bIsEngram");
            Quantity = itemObject.GetPropertyValue<int>("ItemQuantity", 0, 1);
            CraftedByTribe = itemObject.GetPropertyValue<string>("CrafterTribeName");
            CraftedByPlayer = itemObject.GetPropertyValue<string>("CrafterCharacterName");
            UploadedTimeInGame = 0;
            UploadedTime = null;


            if (itemObject.HasAnyProperty("ItemRating") & !ClassName.ToLower().Contains("egg"))
            {
                var ratingProp = itemObject.GetTypedProperty<PropertyFloat>("ItemRating")?.Value;
                if (!float.IsNaN(ratingProp.Value))
                {
                    Rating = ratingProp.Value;
                }
                else
                {
                    Rating = 0.0001f;
                }

            }

        }

        public ContentItem()
        {

        }

    }
}
