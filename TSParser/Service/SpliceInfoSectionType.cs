// Copyright 2021 Eldar Nizamutdinov deim.mobile<at>gmail.com 
//  
// Licensed under the Apache License, Version 2.0 (the "License")
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at 
//
//     http://www.apache.org/licenses/LICENSE-2.0 
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System.Xml.Serialization;
using TSParser.Descriptors.Scte35Descriptors;
using TSParser.Service;
using TSParser.Tables.DvbTables;
using TSParser.Tables.Scte35;


[Serializable]
[System.ComponentModel.DesignerCategory("code")]
[XmlType(Namespace = "http://www.scte.org/schemas/35")]
[XmlRoot("SpliceInfoSection", Namespace = "http://www.scte.org/schemas/35", IsNullable = false)]
public partial class SpliceInfoSectionType
{
    private SpliceInfoSectionTypeEncryptedPacket _encryptedPacketField;
    private SpliceCommandType _spliceCommandType;
    private SpliceDescriptorType[] _spliceDescriptorTypes;
    private ushort _sapTypeField;
    private ulong _ptsAdjustmentField;
    private byte _protocolVersionField;
    private bool _protocolVersionFieldSpecified;
    private ushort _tierField;
    private bool _tierFieldSpecified;

    public SpliceInfoSectionType()
    {
        _sapTypeField = 3;
        _ptsAdjustmentField = ((ulong)(0m));
        _protocolVersionField = 0;
    }
    public SpliceInfoSectionType(SCTE35 scte35)
    {
        
        sapType = scte35.SapType;
        ptsAdjustment = scte35.PtsAdjustment;
        protocolVersion = scte35.ProtocolVersion;
        protocolVersionSpecified = true;
        tier = scte35.Tier;
        tierSpecified = true;
        EncryptedPacket = new SpliceInfoSectionTypeEncryptedPacket(scte35.EncryptedPacket);

        switch (scte35.SpliceCommandItem)
        {
            case BandwidthReservation reservation:
                {
                    Item = new BandwidthReservationType(); break;
                }
            case PrivateCommand prvt:
                {
                    Item = new PrivateCommandType(prvt); break;
                }
            case SpliceInsert insert:
                {
                    Item = new SpliceInsertType(insert); break;
                }
            case SpliceNull spliceNull:
                {
                    Item = new SpliceNullType(); break;
                }
            case SpliceSchedule schedule:
                {
                    Item = new SpliceScheduleType(schedule); break;
                }
            case TimeSignal signal:
                {
                    Item = new TimeSignalType(signal); break;
                }
        }

        Items = new SpliceDescriptorType[scte35.SpliceDescriptorItems.Count];
        for (int i = 0; i < Items.Length; i++)
        {
            switch (scte35.SpliceDescriptorItems[i])
            {
                case AvailDescriptor_0x00 avail:
                    {
                        Items[i] = new AvailDescriptorType(avail); break;
                    }
                case DtmfDescriptor_0x01 dtmf:
                    {
                        Items[i] = new DTMFDescriptorType(dtmf); break;
                    }
                case SegmentationDescriptor_0x02 segmentation:
                    {
                        Items[i] = new SegmentationDescriptorType(segmentation); break;
                    }
                case TimeDescriptor_0x03 time:
                    {
                        Items[i] = new TimeDescriptorType(time); break;
                    }
            }
        }

    }
    // done
    public SpliceInfoSectionTypeEncryptedPacket EncryptedPacket
    {
        get
        {
            return _encryptedPacketField;
        }
        set
        {
            _encryptedPacketField = value;
        }
    }

    [XmlElement("BandwidthReservation", typeof(BandwidthReservationType))]
    [XmlElement("PrivateCommand", typeof(PrivateCommandType))]
    [XmlElement("SpliceInsert", typeof(SpliceInsertType))]
    [XmlElement("SpliceNull", typeof(SpliceNullType))]
    [XmlElement("SpliceSchedule", typeof(SpliceScheduleType))]
    [XmlElement("TimeSignal", typeof(TimeSignalType))]
    public SpliceCommandType Item
    {
        get
        {
            return _spliceCommandType;
        }
        set
        {
            _spliceCommandType = value;
        }
    }

    [XmlElement("AvailDescriptor", typeof(AvailDescriptorType))]
    [XmlElement("DTMFDescriptor", typeof(DTMFDescriptorType))]
    [XmlElement("SegmentationDescriptor", typeof(SegmentationDescriptorType))]
    [XmlElement("TimeDescriptor", typeof(TimeDescriptorType))]
    public SpliceDescriptorType[] Items
    {
        get
        {
            return _spliceDescriptorTypes;
        }
        set
        {
            _spliceDescriptorTypes = value;
        }
    }

    [XmlAttribute]
    public ushort sapType
    {
        get
        {
            return _sapTypeField;
        }
        set
        {
            _sapTypeField = value;
        }
    }

    [XmlAttribute]
    public ulong ptsAdjustment
    {
        get
        {
            return _ptsAdjustmentField;
        }
        set
        {
            _ptsAdjustmentField = value;
        }
    }

    [XmlAttribute]
    public byte protocolVersion
    {
        get
        {
            return _protocolVersionField;
        }
        set
        {
            _protocolVersionField = value;
        }
    }

    [XmlIgnore]
    public bool protocolVersionSpecified
    {
        get
        {
            return _protocolVersionFieldSpecified;
        }
        set
        {
            _protocolVersionFieldSpecified = value;
        }
    }

    [XmlAttribute]
    public ushort tier
    {
        get
        {
            return _tierField;
        }
        set
        {
            _tierField = value;
        }
    }

    [XmlIgnore]
    public bool tierSpecified
    {
        get
        {
            return _tierFieldSpecified;
        }
        set
        {
            _tierFieldSpecified = value;
        }
    }
}


[XmlInclude(typeof(AudioChannelType))]
[XmlInclude(typeof(AudioDescriptorType))]
[XmlInclude(typeof(TimeDescriptorType))]
[XmlInclude(typeof(SegmentationDescriptorType))]
[XmlInclude(typeof(DTMFDescriptorType))]
[XmlInclude(typeof(AvailDescriptorType))]

[Serializable]
[System.ComponentModel.DesignerCategory("code")]
[XmlType(Namespace = "http://www.scte.org/schemas/35")]
public abstract partial class SpliceDescriptorType
{
}



[Serializable]
[System.ComponentModel.DesignerCategory("code")]
[XmlType(Namespace = "http://www.scte.org/schemas/35")]
public partial class AudioDescriptorType : SpliceDescriptorType
{
}


[XmlInclude(typeof(PrivateCommandType))]
[XmlInclude(typeof(BandwidthReservationType))]
[XmlInclude(typeof(TimeSignalType))]
[XmlInclude(typeof(SpliceInsertType))]
[XmlInclude(typeof(SpliceScheduleType))]
[XmlInclude(typeof(SpliceNullType))]

[Serializable]
[System.ComponentModel.DesignerCategory("code")]
[XmlType(Namespace = "http://www.scte.org/schemas/35")]
public class SpliceCommandType
{

}



[Serializable]
[System.ComponentModel.DesignerCategory("code")]
[XmlType(AnonymousType = true, Namespace = "http://www.scte.org/schemas/35")]
public partial class SpliceInfoSectionTypeEncryptedPacket
{
    private byte _encryptionAlgorithmField;
    private byte _cwIndexField;

    [XmlAttribute]
    public byte encryptionAlgorithm
    {
        get
        {
            return _encryptionAlgorithmField;
        }
        set
        {
            _encryptionAlgorithmField = value;
        }
    }

    [XmlAttribute]
    public byte cwIndex
    {
        get
        {
            return _cwIndexField;
        }
        set
        {
            _cwIndexField = value;
        }
    }

    public SpliceInfoSectionTypeEncryptedPacket() { }
    public SpliceInfoSectionTypeEncryptedPacket(EncryptedPacket encryptedPacket)
    {
        encryptionAlgorithm = encryptedPacket.EncryptionAlgorithm;
        cwIndex = encryptedPacket.CwIndex;
    }

}


[Serializable]
[System.ComponentModel.DesignerCategory("code")]
[XmlType(Namespace = "http://www.scte.org/schemas/35")]
[XmlRoot("BandwidthReservation", Namespace = "http://www.scte.org/schemas/35", IsNullable = false)]
public partial class BandwidthReservationType : SpliceCommandType
{
}


[Serializable]
[System.ComponentModel.DesignerCategory("code")]
[XmlType(Namespace = "http://www.scte.org/schemas/35")]
[XmlRoot("PrivateCommand", Namespace = "http://www.scte.org/schemas/35", IsNullable = false)]
public partial class PrivateCommandType : SpliceCommandType
{

    private byte[] _privateBytesField;
    private uint _identifierField;

    [XmlElement(DataType = "hexBinary")]
    public byte[] PrivateBytes
    {
        get
        {
            return _privateBytesField;
        }
        set
        {
            _privateBytesField = value;
        }
    }

    [XmlAttribute]
    public uint identifier
    {
        get
        {
            return _identifierField;
        }
        set
        {
            _identifierField = value;
        }
    }
    public PrivateCommandType() { }
    public PrivateCommandType(PrivateCommand privateCommand)
    {
        identifier = privateCommand.Identifier;
        PrivateBytes = privateCommand.PrivateBytes;
    }
}


[Serializable]
[System.ComponentModel.DesignerCategory("code")]
[XmlType(Namespace = "http://www.scte.org/schemas/35")]
[XmlRoot("SpliceInsert", Namespace = "http://www.scte.org/schemas/35", IsNullable = false)]
public partial class SpliceInsertType : SpliceCommandType
{

    private SpliceInsertTypeBase[] _spliceInserts;
    private BreakDurationType _breakDurationField;
    private uint _spliceEventIdField;
    private bool _spliceEventIdFieldSpecified;
    private bool _spliceEventCancelIndicatorField;
    private bool _outOfNetworkIndicatorField;
    private bool _outOfNetworkIndicatorFieldSpecified;
    private bool _spliceImmediateFlagField;
    private bool _spliceImmediateFlagFieldSpecified;
    private ushort _uniqueProgramIdField;
    private bool _uniqueProgramIdFieldSpecified;
    private byte _availNumField;
    private bool _availNumFieldSpecified;
    private byte _availsExpectedField;
    private bool _availsExpectedFieldSpecified;

    public SpliceInsertType()
    {
        _spliceEventCancelIndicatorField = false;
    }
    public SpliceInsertType(SpliceInsert insert)
    {
        spliceEventId = insert.SpliceEventId;
        spliceEventIdSpecified = true;
        spliceEventCancelIndicator = insert.SpliceEventCancelIndicator;
        if (!spliceEventCancelIndicator)
        {
            outOfNetworkIndicator = insert.OutOfNetworkIndicator;
            outOfNetworkIndicatorSpecified = true;
            spliceImmediateFlag = insert.SpliceImmediateFlag;
            spliceImmediateFlagSpecified = true;
            SpliceInserts = new SpliceInsertTypeBase[insert.SpliceInserts.Length];
            for (int i = 0; i < insert.SpliceInserts.Length; i++)
            {
                if (insert.SpliceInserts[i] is SpliceInsertProgram program)
                {
                    SpliceInserts[i] = new SpliceInsertTypeProgram(program);
                }
                else
                {
                    SpliceInserts[i] = new SpliceInsertTypeComponent(insert.SpliceInserts[i] as SpliceInsertComponent);
                }
            }
            if (insert.DurationFlag)
            {
                BreakDuration = new BreakDurationType(insert.BreakDuration);
            }

        }
        uniqueProgramId = insert.UniqueProgramId;
        uniqueProgramIdSpecified = true;
        availNum = insert.AvailNum;
        availNumSpecified = true;
        availsExpected = insert.AvailsExpected;
        availsExpectedSpecified = true;
    }


    [XmlElement("Component", typeof(SpliceInsertTypeComponent))]
    [XmlElement("Program", typeof(SpliceInsertTypeProgram))]
    public SpliceInsertTypeBase[] SpliceInserts
    {
        get
        {
            return _spliceInserts;
        }
        set
        {
            _spliceInserts = value;
        }
    }

    public BreakDurationType BreakDuration
    {
        get
        {
            return _breakDurationField;
        }
        set
        {
            _breakDurationField = value;
        }
    }

    [XmlAttribute]
    public uint spliceEventId
    {
        get
        {
            return _spliceEventIdField;
        }
        set
        {
            _spliceEventIdField = value;
        }
    }

    [XmlIgnore]
    public bool spliceEventIdSpecified
    {
        get
        {
            return _spliceEventIdFieldSpecified;
        }
        set
        {
            _spliceEventIdFieldSpecified = value;
        }
    }

    [XmlAttribute]
    [System.ComponentModel.DefaultValue(false)]
    public bool spliceEventCancelIndicator
    {
        get
        {
            return _spliceEventCancelIndicatorField;
        }
        set
        {
            _spliceEventCancelIndicatorField = value;
        }
    }

    [XmlAttribute]
    public bool outOfNetworkIndicator
    {
        get
        {
            return _outOfNetworkIndicatorField;
        }
        set
        {
            _outOfNetworkIndicatorField = value;
        }
    }

    [XmlIgnore]
    public bool outOfNetworkIndicatorSpecified
    {
        get
        {
            return _outOfNetworkIndicatorFieldSpecified;
        }
        set
        {
            _outOfNetworkIndicatorFieldSpecified = value;
        }
    }

    [XmlAttribute]
    public bool spliceImmediateFlag
    {
        get
        {
            return _spliceImmediateFlagField;
        }
        set
        {
            _spliceImmediateFlagField = value;
        }
    }

    [XmlIgnore]
    public bool spliceImmediateFlagSpecified
    {
        get
        {
            return _spliceImmediateFlagFieldSpecified;
        }
        set
        {
            _spliceImmediateFlagFieldSpecified = value;
        }
    }

    [XmlAttribute]
    public ushort uniqueProgramId
    {
        get
        {
            return _uniqueProgramIdField;
        }
        set
        {
            _uniqueProgramIdField = value;
        }
    }

    [XmlIgnore]
    public bool uniqueProgramIdSpecified
    {
        get
        {
            return _uniqueProgramIdFieldSpecified;
        }
        set
        {
            _uniqueProgramIdFieldSpecified = value;
        }
    }

    [XmlAttribute]
    public byte availNum
    {
        get
        {
            return _availNumField;
        }
        set
        {
            _availNumField = value;
        }
    }

    [XmlIgnore]
    public bool availNumSpecified
    {
        get
        {
            return _availNumFieldSpecified;
        }
        set
        {
            _availNumFieldSpecified = value;
        }
    }

    [XmlAttribute]
    public byte availsExpected
    {
        get
        {
            return _availsExpectedField;
        }
        set
        {
            _availsExpectedField = value;
        }
    }

    [XmlIgnore]
    public bool availsExpectedSpecified
    {
        get
        {
            return _availsExpectedFieldSpecified;
        }
        set
        {
            _availsExpectedFieldSpecified = value;
        }
    }
}


[Serializable]
[System.ComponentModel.DesignerCategory("code")]
[XmlType(AnonymousType = true, Namespace = "http://www.scte.org/schemas/35")]
public partial class SpliceInsertTypeComponent : SpliceInsertTypeBase
{
    private SpliceTimeType _spliceTimeField;
    private byte _componentTagField;

    public SpliceTimeType SpliceTime
    {
        get
        {
            return _spliceTimeField;
        }
        set
        {
            _spliceTimeField = value;
        }
    }

    [XmlAttribute]
    public byte componentTag
    {
        get
        {
            return _componentTagField;
        }
        set
        {
            _componentTagField = value;
        }
    }
    public SpliceInsertTypeComponent() { }
    public SpliceInsertTypeComponent(SpliceInsertComponent component)
    {
        SpliceTime = new SpliceTimeType(component.SpliceTime);
        componentTag = component.ComponentTag;
    }
}


[Serializable]
[System.ComponentModel.DesignerCategory("code")]
[XmlType(Namespace = "http://www.scte.org/schemas/35")]
[XmlRoot("SpliceTime", Namespace = "http://www.scte.org/schemas/35", IsNullable = false)]
public class SpliceTimeType
{
    private ulong _ptsTimeField;
    private bool _ptsTimeFieldSpecified;

    [XmlAttribute]
    public ulong ptsTime
    {
        get
        {
            return _ptsTimeField;
        }
        set
        {
            _ptsTimeField = value;
        }
    }

    [XmlIgnore]
    public bool ptsTimeSpecified
    {
        get
        {
            return _ptsTimeFieldSpecified;
        }
        set
        {
            _ptsTimeFieldSpecified = value;
        }
    }

    public SpliceTimeType() { }
    public SpliceTimeType(SpliceTime spliceTime)
    {
        ptsTime = spliceTime.PtsTime;
        ptsTimeSpecified = true;
    }
}


[Serializable]
[System.ComponentModel.DesignerCategory("code")]
[XmlType(AnonymousType = true, Namespace = "http://www.scte.org/schemas/35")]
public partial class SpliceInsertTypeProgram : SpliceInsertTypeBase
{
    private SpliceTimeType _spliceTimeField;

    public SpliceTimeType SpliceTime
    {
        get
        {
            return _spliceTimeField;
        }
        set
        {
            _spliceTimeField = value;
        }
    }

    public SpliceInsertTypeProgram() { }
    public SpliceInsertTypeProgram(SpliceInsertProgram program)
    {
        SpliceTime = new SpliceTimeType(program.SpliceTime);
    }
}


[Serializable]
[System.ComponentModel.DesignerCategory("code")]
[XmlType(Namespace = "http://www.scte.org/schemas/35")]
[XmlRoot("BreakDuration", Namespace = "http://www.scte.org/schemas/35", IsNullable = false)]
public partial class BreakDurationType
{
    private bool _autoReturnField;
    private ulong _durationField;

    [XmlAttribute]
    public bool autoReturn
    {
        get
        {
            return _autoReturnField;
        }
        set
        {
            _autoReturnField = value;
        }
    }

    [XmlAttribute]
    public ulong duration
    {
        get
        {
            return _durationField;
        }
        set
        {
            _durationField = value;
        }
    }

    public BreakDurationType() { }
    public BreakDurationType(BreakDuration breakDuration)
    {
        autoReturn = breakDuration.AutoReturn;
        duration = breakDuration.Duration;
    }
}


[Serializable]
[System.ComponentModel.DesignerCategory("code")]
[XmlType(Namespace = "http://www.scte.org/schemas/35")]
[XmlRoot("SpliceNull", Namespace = "http://www.scte.org/schemas/35", IsNullable = false)]
public partial class SpliceNullType : SpliceCommandType
{
}


[Serializable]
[System.ComponentModel.DesignerCategory("code")]
[XmlType(Namespace = "http://www.scte.org/schemas/35")]
[XmlRoot("SpliceSchedule", Namespace = "http://www.scte.org/schemas/35", IsNullable = false)]
public partial class SpliceScheduleType : SpliceCommandType
{

    private SpliceScheduleTypeEvent[] _eventField;

    [XmlElement("Event")]
    public SpliceScheduleTypeEvent[] Event
    {
        get
        {
            return _eventField;
        }
        set
        {
            _eventField = value;
        }
    }
    public SpliceScheduleType() { }
    public SpliceScheduleType(SpliceSchedule schedule)
    {
        Event = new SpliceScheduleTypeEvent[schedule.Events.Length];
        for (int i = 0; i < schedule.Events.Length; i++)
        {
            Event[i] = new SpliceScheduleTypeEvent(schedule.Events[i]);
        }
    }
}


[Serializable]
[System.ComponentModel.DesignerCategory("code")]
[XmlTypeAttribute(AnonymousType = true, Namespace = "http://www.scte.org/schemas/35")]
public partial class SpliceScheduleTypeEvent
{

    private SpliceScheduleTypeEventBase[] _spliceScheduleTypeEvents;
    private BreakDurationType _breakDurationField;
    private uint _spliceEventIdField;
    private bool _spliceEventIdFieldSpecified;
    private bool _spliceEventCancelIndicatorField;
    private bool _outOfNetworkIndicatorField;
    private bool _outOfNetworkIndicatorFieldSpecified;
    private ushort _uniqueProgramIdField;
    private bool _uniqueProgramIdFieldSpecified;
    private byte _availNumField;
    private bool _availNumFieldSpecified;
    private byte _availsExpectedField;
    private bool _availsExpectedFieldSpecified;


    public SpliceScheduleTypeEvent()
    {
        _spliceEventCancelIndicatorField = false;
    }
    public SpliceScheduleTypeEvent(SpliceScheduleEvent evt)
    {
        spliceEventId = evt.SpliceEventId;
        spliceEventIdSpecified = true;
        spliceEventCancelIndicator = evt.SpliceEventCancelIndicator;
        if (!spliceEventCancelIndicator)
        {
            outOfNetworkIndicator = evt.OutOfNetworkIndicator;
            outOfNetworkIndicatorSpecified = true;
            SpliceScheduleTypeEvents = new SpliceScheduleTypeEventBase[evt.SpliceScheduleTypeEvents.Length];
            for (int i = 0; i < SpliceScheduleTypeEvents.Length; i++)
            {
                if (evt.SpliceScheduleTypeEvents[i] is EventProgram program)
                {
                    SpliceScheduleTypeEvents[i] = new SpliceScheduleTypeEventProgram(program);
                }
                else
                {
                    SpliceScheduleTypeEvents[i] = new SpliceScheduleTypeEventComponent(evt.SpliceScheduleTypeEvents[i] as EventComponent);
                }
            }
            if (evt.DurationFlag)
            {
                BreakDuration = new BreakDurationType(evt.BreakDuration);
            }
            uniqueProgramId = evt.UniqueProgramId;
            uniqueProgramIdSpecified = true;
            availNum = evt.AvailNum;
            availNumSpecified = true;
            availsExpected = evt.AvailsExpected;
            availsExpectedSpecified = true;
        }
    }

    [XmlElement("Component", typeof(SpliceScheduleTypeEventComponent))]
    [XmlElement("Program", typeof(SpliceScheduleTypeEventProgram))]
    public SpliceScheduleTypeEventBase[] SpliceScheduleTypeEvents
    {
        get
        {
            return _spliceScheduleTypeEvents;
        }
        set
        {
            _spliceScheduleTypeEvents = value;
        }
    }

    public BreakDurationType BreakDuration
    {
        get
        {
            return _breakDurationField;
        }
        set
        {
            _breakDurationField = value;
        }
    }

    [XmlAttribute]
    public uint spliceEventId
    {
        get
        {
            return _spliceEventIdField;
        }
        set
        {
            _spliceEventIdField = value;
        }
    }

    [XmlIgnore]
    public bool spliceEventIdSpecified
    {
        get
        {
            return _spliceEventIdFieldSpecified;
        }
        set
        {
            _spliceEventIdFieldSpecified = value;
        }
    }

    [XmlAttribute]
    [System.ComponentModel.DefaultValue(false)]
    public bool spliceEventCancelIndicator
    {
        get
        {
            return _spliceEventCancelIndicatorField;
        }
        set
        {
            _spliceEventCancelIndicatorField = value;
        }
    }

    [XmlAttribute]
    public bool outOfNetworkIndicator
    {
        get
        {
            return _outOfNetworkIndicatorField;
        }
        set
        {
            _outOfNetworkIndicatorField = value;
        }
    }

    [XmlIgnore]
    public bool outOfNetworkIndicatorSpecified
    {
        get
        {
            return _outOfNetworkIndicatorFieldSpecified;
        }
        set
        {
            _outOfNetworkIndicatorFieldSpecified = value;
        }
    }

    [XmlAttribute]
    public ushort uniqueProgramId
    {
        get
        {
            return _uniqueProgramIdField;
        }
        set
        {
            _uniqueProgramIdField = value;
        }
    }

    [XmlIgnore]
    public bool uniqueProgramIdSpecified
    {
        get
        {
            return _uniqueProgramIdFieldSpecified;
        }
        set
        {
            _uniqueProgramIdFieldSpecified = value;
        }
    }

    [XmlAttribute]
    public byte availNum
    {
        get
        {
            return _availNumField;
        }
        set
        {
            _availNumField = value;
        }
    }

    [XmlIgnore]
    public bool availNumSpecified
    {
        get
        {
            return _availNumFieldSpecified;
        }
        set
        {
            _availNumFieldSpecified = value;
        }
    }

    [XmlAttribute]
    public byte availsExpected
    {
        get
        {
            return _availsExpectedField;
        }
        set
        {
            _availsExpectedField = value;
        }
    }

    [XmlIgnore]
    public bool availsExpectedSpecified
    {
        get
        {
            return _availsExpectedFieldSpecified;
        }
        set
        {
            _availsExpectedFieldSpecified = value;
        }
    }
}


[Serializable]
[System.ComponentModel.DesignerCategory("code")]
[XmlType(AnonymousType = true, Namespace = "http://www.scte.org/schemas/35")]
public partial class SpliceScheduleTypeEventComponent : SpliceScheduleTypeEventBase
{
    private byte _componentTagField;
    private System.DateTime _utcSpliceTimeField;

    [XmlAttribute]
    public byte componentTag
    {
        get
        {
            return _componentTagField;
        }
        set
        {
            _componentTagField = value;
        }
    }

    [XmlAttribute]
    public System.DateTime utcSpliceTime
    {
        get
        {
            return _utcSpliceTimeField;
        }
        set
        {
            _utcSpliceTimeField = value;
        }
    }
    public SpliceScheduleTypeEventComponent() { }
    public SpliceScheduleTypeEventComponent(EventComponent component)
    {
        componentTag = component.ComponentTag;
        utcSpliceTime = Utils.UnixTimeStampToDateTime(component.UtcSpliceTime);
    }
}


[Serializable]
[System.ComponentModel.DesignerCategory("code")]
[XmlType(AnonymousType = true, Namespace = "http://www.scte.org/schemas/35")]
public partial class SpliceScheduleTypeEventProgram : SpliceScheduleTypeEventBase
{
    private System.DateTime _utcSpliceTimeField;

    [XmlAttribute]
    public System.DateTime utcSpliceTime
    {
        get
        {
            return _utcSpliceTimeField;
        }
        set
        {
            _utcSpliceTimeField = value;
        }
    }
    public SpliceScheduleTypeEventProgram() { }
    public SpliceScheduleTypeEventProgram(EventProgram program)
    {
        utcSpliceTime = Utils.UnixTimeStampToDateTime(program.UtcSpliceTime);
    }
}


[Serializable]
[System.ComponentModel.DesignerCategory("code")]
[XmlType(Namespace = "http://www.scte.org/schemas/35")]
[XmlRoot("TimeSignal", Namespace = "http://www.scte.org/schemas/35", IsNullable = false)]
public class TimeSignalType : SpliceCommandType
{
    private SpliceTimeType _spliceTimeField;
    public SpliceTimeType SpliceTime
    {
        get
        {
            return _spliceTimeField;
        }
        set
        {
            _spliceTimeField = value;
        }
    }
    public TimeSignalType() { }
    public TimeSignalType(TimeSignal timeSignal)
    {
        SpliceTime = new SpliceTimeType(timeSignal.SpliceTime);
    }
}


[Serializable]
[System.ComponentModel.DesignerCategory("code")]
[XmlType(Namespace = "http://www.scte.org/schemas/35")]
[XmlRoot("AvailDescriptor", Namespace = "http://www.scte.org/schemas/35", IsNullable = false)]
public partial class AvailDescriptorType : SpliceDescriptorType
{
    private uint _providerAvailIdField;

    [XmlAttribute]
    public uint providerAvailId
    {
        get
        {
            return _providerAvailIdField;
        }
        set
        {
            _providerAvailIdField = value;
        }
    }
    public AvailDescriptorType() { }
    public AvailDescriptorType(AvailDescriptor_0x00 desc)
    {
        providerAvailId = desc.ProviderAvailId;
    }
}


[Serializable]

[System.ComponentModel.DesignerCategory("code")]
[XmlType(Namespace = "http://www.scte.org/schemas/35")]
[XmlRoot("DTMFDescriptor", Namespace = "http://www.scte.org/schemas/35", IsNullable = false)]
public class DTMFDescriptorType : SpliceDescriptorType
{
    private byte _prerollField;
    private bool _prerollFieldSpecified;
    private string charsField;

    [XmlAttribute]
    public byte preroll
    {
        get
        {
            return _prerollField;
        }
        set
        {
            _prerollField = value;
        }
    }

    [XmlIgnore]
    public bool prerollSpecified
    {
        get
        {
            return _prerollFieldSpecified;
        }
        set
        {
            _prerollFieldSpecified = value;
        }
    }

    [XmlAttribute(DataType = "token")]
    public string chars
    {
        get
        {
            return charsField;
        }
        set
        {
            charsField = value;
        }
    }
    public DTMFDescriptorType() { }
    public DTMFDescriptorType(DtmfDescriptor_0x01 desc)
    {
        preroll = desc.Preroll;
        prerollSpecified = true;
        chars = desc.DtmfChar;
    }
}

[Serializable]
[System.ComponentModel.DesignerCategory("code")]
[XmlType(Namespace = "http://www.scte.org/schemas/35")]
[XmlRoot("SegmentationDescriptor", Namespace = "http://www.scte.org/schemas/35", IsNullable = false)]
public partial class SegmentationDescriptorType : SpliceDescriptorType
{

    private SegmentationDescriptorTypeDeliveryRestrictions _deliveryRestrictionsField;
    private SegmentationUpidType[] _segmentationUpidField;
    private SegmentationDescriptorTypeComponent[] _componentField;
    private uint _segmentationEventIdField;
    private bool _segmentationEventIdFieldSpecified;
    private bool _segmentationEventCancelIndicatorField;
    private ulong _segmentationDurationField;
    private bool _segmentationDurationFieldSpecified;
    private byte _segmentationTypeIdField;
    private bool _segmentationTypeIdFieldSpecified;
    private byte _segmentNumField;
    private bool _segmentNumFieldSpecified;
    private byte _segmentsExpectedField;
    private bool _segmentsExpectedFieldSpecified;
    private byte _subSegmentNumField;
    private bool _subSegmentNumFieldSpecified;
    private byte _subSegmentsExpectedField;
    private bool _subSegmentsExpectedFieldSpecified;

    public SegmentationDescriptorType()
    {
        _segmentationEventCancelIndicatorField = false;
    }
    public SegmentationDescriptorType(SegmentationDescriptor_0x02 desc)
    {
        segmentationEventId = desc.SegmentationEventId;
        segmentationEventIdSpecified = true;
        segmentationEventCancelIndicator = desc.SegmentationEventCancelIndicator;

        if (!segmentationEventCancelIndicator)
        {
            if (!desc.DeliveryNotRestictedFlag)
            {
                DeliveryRestrictions = new SegmentationDescriptorTypeDeliveryRestrictions(desc.DeliveryRestrictions);
            }
            if (!desc.ProgramSegmentationFlag)
            {
                Component = new SegmentationDescriptorTypeComponent[desc.Components.Length];
                for (int i = 0; i < desc.Components.Length; i++)
                {
                    Component[i] = new SegmentationDescriptorTypeComponent(desc.Components[i]);
                }
            }
            if (desc.SegmentationDurationFlag)
            {
                segmentationDuration = desc.SegmentationDuration;
                segmentationDurationSpecified = true;
            }

            SegmentationUpid = new SegmentationUpidType[desc.SegmentationUpids.Length];

            for (int i = 0; i < desc.SegmentationUpids.Length; i++)
            {
                SegmentationUpid[i] = new SegmentationUpidType(desc.SegmentationUpids[i]);
            }
            segmentationTypeId = desc.SegmentationTypeId;
            segmentationTypeIdSpecified = true;
            segmentNum = desc.SegmentNum;
            segmentNumSpecified = true;
            segmentsExpected = desc.SegmentsExpected;
            segmentsExpectedSpecified = true;

            if (segmentationTypeId == 0x34 ||
                    segmentationTypeId == 0x36 ||
                    segmentationTypeId == 0x38 ||
                    segmentationTypeId == 0x3A)
            {
                subSegmentNum = desc.SubSegmentNum;
                subSegmentNumSpecified = true;
                subSegmentsExpected = desc.SubSegmentsExpected;
                subSegmentsExpectedSpecified = true;
            }
        }
    }


    public SegmentationDescriptorTypeDeliveryRestrictions DeliveryRestrictions
    {
        get
        {
            return _deliveryRestrictionsField;
        }
        set
        {
            _deliveryRestrictionsField = value;
        }
    }

    [XmlElement("SegmentationUpid")]
    public SegmentationUpidType[] SegmentationUpid
    {
        get
        {
            return _segmentationUpidField;
        }
        set
        {
            _segmentationUpidField = value;
        }
    }

    [XmlElement("Component")]
    public SegmentationDescriptorTypeComponent[] Component
    {
        get
        {
            return _componentField;
        }
        set
        {
            _componentField = value;
        }
    }

    [XmlAttribute]
    public uint segmentationEventId
    {
        get
        {
            return _segmentationEventIdField;
        }
        set
        {
            _segmentationEventIdField = value;
        }
    }

    [XmlIgnore]
    public bool segmentationEventIdSpecified
    {
        get
        {
            return _segmentationEventIdFieldSpecified;
        }
        set
        {
            _segmentationEventIdFieldSpecified = value;
        }
    }

    [XmlAttribute]
    public bool segmentationEventCancelIndicator
    {
        get
        {
            return _segmentationEventCancelIndicatorField;
        }
        set
        {
            _segmentationEventCancelIndicatorField = value;
        }
    }

    [XmlAttribute]
    public ulong segmentationDuration
    {
        get
        {
            return _segmentationDurationField;
        }
        set
        {
            _segmentationDurationField = value;
        }
    }

    [XmlIgnore]
    public bool segmentationDurationSpecified
    {
        get
        {
            return _segmentationDurationFieldSpecified;
        }
        set
        {
            _segmentationDurationFieldSpecified = value;
        }
    }

    [XmlAttribute]
    public byte segmentationTypeId
    {
        get
        {
            return _segmentationTypeIdField;
        }
        set
        {
            _segmentationTypeIdField = value;
        }
    }

    [XmlIgnore]
    public bool segmentationTypeIdSpecified
    {
        get
        {
            return _segmentationTypeIdFieldSpecified;
        }
        set
        {
            _segmentationTypeIdFieldSpecified = value;
        }
    }

    [XmlAttribute]
    public byte segmentNum
    {
        get
        {
            return _segmentNumField;
        }
        set
        {
            _segmentNumField = value;
        }
    }

    [XmlIgnore]
    public bool segmentNumSpecified
    {
        get
        {
            return _segmentNumFieldSpecified;
        }
        set
        {
            _segmentNumFieldSpecified = value;
        }
    }

    [XmlAttribute]
    public byte segmentsExpected
    {
        get
        {
            return _segmentsExpectedField;
        }
        set
        {
            _segmentsExpectedField = value;
        }
    }

    [XmlIgnore]
    public bool segmentsExpectedSpecified
    {
        get
        {
            return _segmentsExpectedFieldSpecified;
        }
        set
        {
            _segmentsExpectedFieldSpecified = value;
        }
    }

    [XmlAttribute]
    public byte subSegmentNum
    {
        get
        {
            return _subSegmentNumField;
        }
        set
        {
            _subSegmentNumField = value;
        }
    }

    [XmlIgnore]
    public bool subSegmentNumSpecified
    {
        get
        {
            return _subSegmentNumFieldSpecified;
        }
        set
        {
            _subSegmentNumFieldSpecified = value;
        }
    }

    [XmlAttribute]
    public byte subSegmentsExpected
    {
        get
        {
            return _subSegmentsExpectedField;
        }
        set
        {
            _subSegmentsExpectedField = value;
        }
    }

    [XmlIgnore]
    public bool subSegmentsExpectedSpecified
    {
        get
        {
            return _subSegmentsExpectedFieldSpecified;
        }
        set
        {
            _subSegmentsExpectedFieldSpecified = value;
        }
    }
}


[Serializable]
[System.ComponentModel.DesignerCategory("code")]
[XmlType(AnonymousType = true, Namespace = "http://www.scte.org/schemas/35")]
public partial class SegmentationDescriptorTypeDeliveryRestrictions
{
    private bool _webDeliveryAllowedFlagField;
    private bool _noRegionalBlackoutFlagField;
    private bool _archiveAllowedFlagField;
    private byte _deviceRestrictionsField;

    [XmlAttribute]
    public bool webDeliveryAllowedFlag
    {
        get
        {
            return _webDeliveryAllowedFlagField;
        }
        set
        {
            _webDeliveryAllowedFlagField = value;
        }
    }

    [XmlAttribute]
    public bool noRegionalBlackoutFlag
    {
        get
        {
            return _noRegionalBlackoutFlagField;
        }
        set
        {
            _noRegionalBlackoutFlagField = value;
        }
    }

    [XmlAttribute]
    public bool archiveAllowedFlag
    {
        get
        {
            return _archiveAllowedFlagField;
        }
        set
        {
            _archiveAllowedFlagField = value;
        }
    }

    [XmlAttribute]
    public byte deviceRestrictions
    {
        get
        {
            return _deviceRestrictionsField;
        }
        set
        {
            _deviceRestrictionsField = value;
        }
    }
    public SegmentationDescriptorTypeDeliveryRestrictions() { }
    public SegmentationDescriptorTypeDeliveryRestrictions(DeliveryRestrictions restrictions)
    {
        webDeliveryAllowedFlag = restrictions.WebDeliveryAllowedFlag;
        noRegionalBlackoutFlag = restrictions.NoRegionalBlackoutFlag;
        archiveAllowedFlag = restrictions.ArchiveAllowedFlag;
        deviceRestrictions = restrictions.DeviceRestrictions;
    }

}


[Serializable]
[System.ComponentModel.DesignerCategory("code")]
[XmlType(Namespace = "http://www.scte.org/schemas/35")]
[XmlRoot("SegmentationUpid", Namespace = "http://www.scte.org/schemas/35", IsNullable = false)]
public partial class SegmentationUpidType
{
    private byte _segmentationUpidTypeField;
    private bool _segmentationUpidTypeFieldSpecified;
    private uint _formatIdentifierField;
    private bool _formatIdentifierFieldSpecified;
    private string _segmentationUpidFormatField;
    private string _valueField;

    [XmlAttribute]
    public byte segmentationUpidType
    {
        get
        {
            return _segmentationUpidTypeField;
        }
        set
        {
            _segmentationUpidTypeField = value;
        }
    }

    [XmlIgnore]
    public bool segmentationUpidTypeSpecified
    {
        get
        {
            return _segmentationUpidTypeFieldSpecified;
        }
        set
        {
            _segmentationUpidTypeFieldSpecified = value;
        }
    }

    [XmlAttribute]
    public uint formatIdentifier
    {
        get
        {
            return _formatIdentifierField;
        }
        set
        {
            _formatIdentifierField = value;
        }
    }

    [XmlIgnore]
    public bool formatIdentifierSpecified
    {
        get
        {
            return _formatIdentifierFieldSpecified;
        }
        set
        {
            _formatIdentifierFieldSpecified = value;
        }
    }

    [XmlAttribute]
    public string segmentationUpidFormat
    {
        get
        {
            return _segmentationUpidFormatField;
        }
        set
        {
            _segmentationUpidFormatField = value;
        }
    }

    [XmlText(DataType = "token")]
    public string Value
    {
        get
        {
            return _valueField;
        }
        set
        {
            _valueField = value;
        }
    }

    public SegmentationUpidType() { }
    public SegmentationUpidType(SegmentationUpid upid)
    {
        segmentationUpidType = upid.segmentationUpidType;
        segmentationUpidTypeSpecified = true;
        segmentationUpidFormat = upid.SegmentationUpidFormat;
        Value = upid.Value;

        if (upid.segmentationUpidType == 0x0C)
        {
            formatIdentifier = upid.FormatIdentifier;
            formatIdentifierSpecified = true;
        }
    }
}


[Serializable]
[System.ComponentModel.DesignerCategory("code")]
[XmlType(AnonymousType = true, Namespace = "http://www.scte.org/schemas/35")]
public class SegmentationDescriptorTypeComponent
{
    private byte _componentTagField;
    private ulong _ptsOffsetField;

    [XmlAttribute]
    public byte componentTag
    {
        get
        {
            return _componentTagField;
        }
        set
        {
            _componentTagField = value;
        }
    }
    [XmlAttribute]
    public ulong ptsOffset
    {
        get
        {
            return _ptsOffsetField;
        }
        set
        {
            _ptsOffsetField = value;
        }
    }

    public SegmentationDescriptorTypeComponent() { }
    public SegmentationDescriptorTypeComponent(SegmentationComponent component)
    {
        componentTag = component.ComponentTag;
        ptsOffset = component.PtsOffset;
    }
}


[Serializable]
[System.ComponentModel.DesignerCategory("code")]
[XmlType(Namespace = "http://www.scte.org/schemas/35")]
[XmlRoot("TimeDescriptor", Namespace = "http://www.scte.org/schemas/35", IsNullable = false)]
public partial class TimeDescriptorType : SpliceDescriptorType
{
    private ulong _taiSecondsField;
    private bool _taiSecondsFieldSpecified;
    private uint _taiNsField;
    private bool _taiNsFieldSpecified;
    private ushort _utcOffsetField;
    private bool _utcOffsetFieldSpecified;

    [XmlAttribute]
    public ulong taiSeconds
    {
        get
        {
            return _taiSecondsField;
        }
        set
        {
            _taiSecondsField = value;
        }
    }
    [XmlIgnore]
    public bool taiSecondsSpecified
    {
        get
        {
            return _taiSecondsFieldSpecified;
        }
        set
        {
            _taiSecondsFieldSpecified = value;
        }
    }
    [XmlAttribute]
    public uint taiNs
    {
        get
        {
            return _taiNsField;
        }
        set
        {
            _taiNsField = value;
        }
    }
    [XmlIgnore]
    public bool taiNsSpecified
    {
        get
        {
            return _taiNsFieldSpecified;
        }
        set
        {
            _taiNsFieldSpecified = value;
        }
    }
    [XmlAttribute]
    public ushort utcOffset
    {
        get
        {
            return _utcOffsetField;
        }
        set
        {
            _utcOffsetField = value;
        }
    }
    [XmlIgnore]
    public bool utcOffsetSpecified
    {
        get
        {
            return _utcOffsetFieldSpecified;
        }
        set
        {
            _utcOffsetFieldSpecified = value;
        }
    }

    public TimeDescriptorType() { }
    public TimeDescriptorType(TimeDescriptor_0x03 desc)
    {
        taiSeconds = desc.TaiSeconds;
        taiNsSpecified = true;
        taiNs = desc.TaiNs;
        taiSecondsSpecified = true;
        utcOffset = desc.UtcOffset;
        utcOffsetSpecified = true;
    }
}

[Serializable]
[System.ComponentModel.DesignerCategory("code")]
[XmlType(Namespace = "http://www.scte.org/schemas/35")]
[XmlRoot("Binary", Namespace = "http://www.scte.org/schemas/35", IsNullable = false)]
public partial class BinaryType
{
    private string _signalTypeField;
    private byte[] _valueField;

    public BinaryType()
    {
        _signalTypeField = "SpliceInfoSection";
    }

    [XmlAttribute(DataType = "token")]
    [System.ComponentModel.DefaultValue("SpliceInfoSection")]
    public string signalType
    {
        get
        {
            return _signalTypeField;
        }
        set
        {
            _signalTypeField = value;
        }
    }

    [XmlText(DataType = "base64Binary")]
    public byte[] Value
    {
        get
        {
            return _valueField;
        }
        set
        {
            _valueField = value;
        }
    }
}


[Serializable]
[System.ComponentModel.DesignerCategory("code")]
[XmlType(Namespace = "http://www.scte.org/schemas/35")]
[XmlRoot("AudioChannel", Namespace = "http://www.scte.org/schemas/35", IsNullable = false)]
public partial class AudioChannelType : SpliceDescriptorType
{
    private string _iSOCodeField;
    private byte _bitStreamModeField;
    private byte _numChannelsField;
    private byte _fullSrvcAudioField;
    private byte _componentTagField;
    private bool _componentTagFieldSpecified;


    [XmlAttribute(DataType = "language")]
    public string ISOCode
    {
        get
        {
            return _iSOCodeField;
        }
        set
        {
            _iSOCodeField = value;
        }
    }
    [XmlAttribute]
    public byte BitStreamMode
    {
        get
        {
            return _bitStreamModeField;
        }
        set
        {
            _bitStreamModeField = value;
        }
    }
    [XmlAttribute]
    public byte NumChannels
    {
        get
        {
            return _numChannelsField;
        }
        set
        {
            _numChannelsField = value;
        }
    }
    [XmlAttribute]
    public byte FullSrvcAudio
    {
        get
        {
            return _fullSrvcAudioField;
        }
        set
        {
            _fullSrvcAudioField = value;
        }
    }
    [XmlAttribute]
    public byte componentTag
    {
        get
        {
            return _componentTagField;
        }
        set
        {
            _componentTagField = value;
        }
    }
    [XmlIgnore]
    public bool componentTagSpecified
    {
        get
        {
            return _componentTagFieldSpecified;
        }
        set
        {
            _componentTagFieldSpecified = value;
        }
    }
    public AudioChannelType() { }
    public AudioChannelType(AudioChanType channel)
    {
        ISOCode = channel.ISOCode;
        BitStreamMode = channel.BitStreamMode;
        NumChannels = channel.NumChannels;
        FullSrvcAudio = channel.FullSrvcAudio;
        componentTag = channel.ComponentTag;
        componentTagSpecified = true;
    }
}

[Serializable]
[System.ComponentModel.DesignerCategory("code")]
[XmlType(AnonymousType = true, Namespace = "http://www.scte.org/schemas/35")]
[XmlRoot(Namespace = "http://www.scte.org/schemas/35", IsNullable = false)]
public partial class AudioDescriptor : AudioDescriptorType
{
    private AudioChannelType[] _audioChannelField;

    [XmlElement("AudioChannel")]
    public AudioChannelType[] AudioChannel
    {
        get
        {
            return _audioChannelField;
        }
        set
        {
            _audioChannelField = value;
        }
    }
    public AudioDescriptor() { }
    public AudioDescriptor(AudioDescriptor_0x04 desc)
    {
        AudioChannel = new AudioChannelType[desc.AudioCount];
        for (int i = 0; i < desc.AudioCount; i++)
        {
            AudioChannel[i] = new AudioChannelType(desc.AudioChannels[i]);
        }
    }
}

[XmlInclude(typeof(SpliceScheduleTypeEventComponent))]
[XmlInclude(typeof(SpliceScheduleTypeEventProgram))]
[Serializable]

[System.ComponentModel.DesignerCategory("code")]
[XmlType(Namespace = "http://www.scte.org/schemas/35")]
public class SpliceScheduleTypeEventBase
{

}

[XmlInclude(typeof(SpliceInsertTypeComponent))]
[XmlInclude(typeof(SpliceInsertTypeProgram))]
[Serializable]

[System.ComponentModel.DesignerCategory("code")]
[XmlType(Namespace = "http://www.scte.org/schemas/35")]
public class SpliceInsertTypeBase
{

}


