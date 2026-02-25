public static class EEBusMessages
{
    public static string MsgNodeManagementDetailedDiscoveryDataReply = """
{
  "data": {
    "header": {
      "protocolId": "ee1.0"
    },
    "payload": {
      "datagram": {
        "header": {
          "specificationVersion": "1.3.0",
          "addressSource": {
            "device": "d:_n:eebus-tester-tui_eebus-tester-tui-123456",
            "entity": [0],
            "feature": 0
          },
          "addressDestination": {
            "device": "Kermi-EEBUS-Demo-Client",
            "entity": [0],
            "feature": 0
          },
          "msgCounter": 2,
          "msgCounterReference": 1,
          "cmdClassifier": "reply"
        },
        "payload": {
          "cmd": [
            {
              "nodeManagementDetailedDiscoveryData": {
                "specificationVersionList": {
                  "specificationVersion": ["1.3.0"]
                },
                "deviceInformation": {
                  "description": {
                    "deviceAddress": {
                      "device": "d:_n:eebus-tester-tui_eebus-tester-tui-123456"
                    },
                    "deviceType": "EnergyManagementSystem",
                    "networkFeatureSet": "smart"
                  }
                },
                "entityInformation": [
                  {
                    "description": {
                      "entityAddress": { "entity": [0] },
                      "entityType": "DeviceInformation"
                    }
                  },
                  {
                    "description": {
                      "entityAddress": { "entity": [1] },
                      "entityType": "GridGuard"
                    }
                  }
                ],
                "featureInformation": [
                  {
                    "description": {
                      "featureAddress": {
                        "device": "d:_n:eebus-tester-tui_eebus-tester-tui-123456",
                        "entity": [0],
                        "feature": 0
                      },
                      "featureType": "NodeManagement",
                      "role": "special",
                      "supportedFunction": [
                        { "function": "nodeManagementDestinationListData", "possibleOperations": { "read": {} } },
                        { "function": "nodeManagementUseCaseData", "possibleOperations": { "read": {} } },
                        { "function": "nodeManagementSubscriptionRequestCall", "possibleOperations": {} },
                        { "function": "nodeManagementBindingData", "possibleOperations": { "read": {} } },
                        { "function": "nodeManagementBindingRequestCall", "possibleOperations": {} },
                        { "function": "nodeManagementDetailedDiscoveryData", "possibleOperations": { "read": {} } },
                        { "function": "nodeManagementSubscriptionData", "possibleOperations": { "read": {} } },
                        { "function": "nodeManagementSubscriptionDeleteCall", "possibleOperations": {} },
                        { "function": "nodeManagementBindingDeleteCall", "possibleOperations": {} }
                      ]
                    }
                  },
                  {
                    "description": {
                      "featureAddress": {
                        "device": "d:_n:eebus-tester-tui_eebus-tester-tui-123456",
                        "entity": [0],
                        "feature": 1
                      },
                      "featureType": "DeviceClassification",
                      "role": "server",
                      "supportedFunction": [
                        { "function": "deviceClassificationManufacturerData", "possibleOperations": { "read": {} } }
                      ]
                    }
                  },
                  {
                    "description": {
                      "featureAddress": {
                        "device": "d:_n:eebus-tester-tui_eebus-tester-tui-123456",
                        "entity": [1],
                        "feature": 1
                      },
                      "featureType": "DeviceDiagnosis",
                      "role": "client",
                      "description": "DeviceDiagnosis Client"
                    }
                  },
                  {
                    "description": {
                      "featureAddress": {
                        "device": "d:_n:eebus-tester-tui_eebus-tester-tui-123456",
                        "entity": [1],
                        "feature": 2
                      },
                      "featureType": "LoadControl",
                      "role": "client",
                      "description": "LoadControl Client"
                    }
                  },
                  {
                    "description": {
                      "featureAddress": {
                        "device": "d:_n:eebus-tester-tui_eebus-tester-tui-123456",
                        "entity": [1],
                        "feature": 3
                      },
                      "featureType": "DeviceConfiguration",
                      "role": "client",
                      "description": "DeviceConfiguration Client"
                    }
                  },
                  {
                    "description": {
                      "featureAddress": {
                        "device": "d:_n:eebus-tester-tui_eebus-tester-tui-123456",
                        "entity": [1],
                        "feature": 4
                      },
                      "featureType": "ElectricalConnection",
                      "role": "client",
                      "description": "ElectricalConnection Client"
                    }
                  },
                  {
                    "description": {
                      "featureAddress": {
                        "device": "d:_n:eebus-tester-tui_eebus-tester-tui-123456",
                        "entity": [1],
                        "feature": 5
                      },
                      "featureType": "DeviceDiagnosis",
                      "role": "server",
                      "supportedFunction": [
                        { "function": "deviceDiagnosisHeartbeatData", "possibleOperations": { "read": {} } }
                      ],
                      "description": "DeviceDiagnosis Server"
                    }
                  }
                ]
              }
            }
          ]
        }
      }
    }
  },
  "extension": null
}
""";


    public static string MsgNodeManagementSubscriptionRequestCall_1 = """
{
  "data": {
    "header": {
      "protocolId": "ee1.0"
    },
    "payload": {
      "datagram": {
        "header": {
          "specificationVersion": "1.3.0",
          "addressSource": {
            "device": "d:_n:eebus-tester-tui_eebus-tester-tui-123456",
            "entity": [0],
            "feature": 0
          },
          "addressDestination": {
            "device": "Kermi-EEBUS-Demo-Client",
            "entity": [0],
            "feature": 0
          },
          "msgCounter": 3,
          "cmdClassifier": "call",
          "ackRequest": true
        },
        "payload": {
          "cmd": [
            {
              "nodeManagementSubscriptionRequestCall": {
                "subscriptionRequest": {
                  "clientAddress": {
                    "device": "d:_n:eebus-tester-tui_eebus-tester-tui-123456",
                    "entity": [0],
                    "feature": 0
                  },
                  "serverAddress": {
                    "device": "Kermi-EEBUS-Demo-Client",
                    "entity": [0],
                    "feature": 0
                  },
                  "serverFeatureType": "NodeManagement"
                }
              }
            }
          ]
        }
      }
    }
  },
  "extension": null
}
""";
    public static string MsgNodeManagementUseCaseData = """
{
  "data": {
    "header": {
      "protocolId": "ee1.0"
    },
    "payload": {
      "datagram": {
        "header": {
          "specificationVersion": "1.3.0",
          "addressSource": {
            "device": "d:_n:eebus-tester-tui_eebus-tester-tui-123456",
            "entity": [0],
            "feature": 0
          },
          "addressDestination": {
            "device": "Kermi-EEBUS-Demo-Client",
            "entity": [0],
            "feature": 0
          },
          "msgCounter": 4,
          "cmdClassifier": "read"
        },
        "payload": {
          "cmd": [
            {
              "nodeManagementUseCaseData": {}
            }
          ]
        }
      }
    }
  },
  "extension": null
}
""";

    public static string MsgNodeManagementSubscriptionRequestCall_LoadControl = """
{
  "data": {
    "header": {
      "protocolId": "ee1.0"
    },
    "payload": {
      "datagram": {
        "header": {
          "specificationVersion": "1.3.0",
          "addressSource": {
            "device": "d:_n:eebus-tester-tui_eebus-tester-tui-123456",
            "entity": [0],
            "feature": 0
          },
          "addressDestination": {
            "device": "Kermi-EEBUS-Demo-Client",
            "entity": [0],
            "feature": 0
          },
          "msgCounter": 5,
          "cmdClassifier": "call",
          "ackRequest": true
        },
        "payload": {
          "cmd": [
            {
              "nodeManagementSubscriptionRequestCall": {
                "subscriptionRequest": {
                  "clientAddress": {
                    "device": "d:_n:eebus-tester-tui_eebus-tester-tui-123456",
                    "entity": [1],
                    "feature": 2
                  },
                  "serverAddress": {
                    "device": "Kermi-EEBUS-Demo-Client",
                    "entity": [1],
                    "feature": 2
                  },
                  "serverFeatureType": "LoadControl"
                }
              }
            }
          ]
        }
      }
    }
  },
  "extension": null
}
""";
    public static string MsgNodeManagementBindingRequestCall = """
{
  "data": {
    "header": {
      "protocolId": "ee1.0"
    },
    "payload": {
      "datagram": {
        "header": {
          "specificationVersion": "1.3.0",
          "addressSource": {
            "device": "d:_n:eebus-tester-tui_eebus-tester-tui-123456",
            "entity": [0],
            "feature": 0
          },
          "addressDestination": {
            "device": "Kermi-EEBUS-Demo-Client",
            "entity": [0],
            "feature": 0
          },
          "msgCounter": 6,
          "cmdClassifier": "call",
          "ackRequest": true
        },
        "payload": {
          "cmd": [
            {
              "nodeManagementBindingRequestCall": {
                "bindingRequest": {
                  "clientAddress": {
                    "device": "d:_n:eebus-tester-tui_eebus-tester-tui-123456",
                    "entity": [1],
                    "feature": 2
                  },
                  "serverAddress": {
                    "device": "Kermi-EEBUS-Demo-Client",
                    "entity": [1],
                    "feature": 2
                  },
                  "serverFeatureType": "LoadControl"
                }
              }
            }
          ]
        }
      }
    }
  },
  "extension": null
}
""";


    public static string MsgLoadControlLimitDescriptionListDataRead = """
{
  "data": {
    "header": {
      "protocolId": "ee1.0"
    },
    "payload": {
      "datagram": {
        "header": {
          "specificationVersion": "1.3.0",
          "addressSource": {
            "device": "d:_n:eebus-tester-tui_eebus-tester-tui-123456",
            "entity": [1],
            "feature": 2
          },
          "addressDestination": {
            "device": "Kermi-EEBUS-Demo-Client",
            "entity": [1],
            "feature": 2
          },
          "msgCounter": 7,
          "cmdClassifier": "read"
        },
        "payload": {
          "cmd": [
            {
              "loadControlLimitDescriptionListData": {}
            }
          ]
        }
      }
    }
  },
  "extension": null
}
""";
    public static string MsgDeviceConfigurationKeyValueDescriptionListData = """
{
  "data": {
    "header": {
      "protocolId": "ee1.0"
    },
    "payload": {
      "datagram": {
        "header": {
          "specificationVersion": "1.3.0",
          "addressSource": {
            "device": "d:_n:eebus-tester-tui_eebus-tester-tui-123456",
            "entity": [1],
            "feature": 3
          },
          "addressDestination": {
            "device": "Kermi-EEBUS-Demo-Client",
            "entity": [1],
            "feature": 3
          },
          "msgCounter": 10,
          "cmdClassifier": "read"
        },
        "payload": {
          "cmd": [
            {
              "deviceConfigurationKeyValueDescriptionListData": {}
            }
          ]
        }
      }
    }
  },
  "extension": null
}
""";
    public static string MsgDeviceDiagnosisHeartbeatDataRead = """
{
  "data": {
    "header": {
      "protocolId": "ee1.0"
    },
    "payload": {
      "datagram": {
        "header": {
          "specificationVersion": "1.3.0",
          "addressSource": {
            "device": "d:_n:eebus-tester-tui_eebus-tester-tui-123456",
            "entity": [1],
            "feature": 1
          },
          "addressDestination": {
            "device": "Kermi-EEBUS-Demo-Client",
            "entity": [1],
            "feature": 4
          },
          "msgCounter": 12,
          "cmdClassifier": "read"
        },
        "payload": {
          "cmd": [
            {
              "deviceDiagnosisHeartbeatData": {
                "timestamp": null,
                "heartbeatCounter": null,
                "heartbeatTimeout": null
              }
            }
          ]
        }
      }
    }
  },
  "extension": null
}
""";
    public static string MsgLoadControlLimitListDataRead = """
{
  "data": {
    "header": {
      "protocolId": "ee1.0"
    },
    "payload": {
      "datagram": {
        "header": {
          "specificationVersion": "1.3.0",
          "addressSource": {
            "device": "d:_n:eebus-tester-tui_eebus-tester-tui-123456",
            "entity": [1],
            "feature": 2
          },
          "addressDestination": {
            "device": "Kermi-EEBUS-Demo-Client",
            "entity": [1],
            "feature": 2
          },
          "msgCounter": 13,
          "cmdClassifier": "read"
        },
        "payload": {
          "cmd": [
            {
              "loadControlLimitListData": {}
            }
          ]
        }
      }
    }
  },
  "extension": null
}
""";
    public static string MsgDeviceConfigurationKeyValueListDataRead = """
{
  "data": {
    "header": {
      "protocolId": "ee1.0"
    },
    "payload": {
      "datagram": {
        "header": {
          "specificationVersion": "1.3.0",
          "addressSource": {
            "device": "d:_n:eebus-tester-tui_eebus-tester-tui-123456",
            "entity": [1],
            "feature": 3
          },
          "addressDestination": {
            "device": "Kermi-EEBUS-Demo-Client",
            "entity": [1],
            "feature": 3
          },
          "msgCounter": 14,
          "cmdClassifier": "read"
        },
        "payload": {
          "cmd": [
            {
              "deviceConfigurationKeyValueListData": {}
            }
          ]
        }
      }
    }
  },
  "extension": null
}
""";
    public static string MsgLoadControlLimitListDataWrite = """
{
  "data": {
    "header": {
      "protocolId": "ee1.0"
    },
    "payload": {
      "datagram": {
        "header": {
          "specificationVersion": "1.3.0",
          "addressSource": {
            "device": "d:_n:eebus-tester-tui_eebus-tester-tui-123456",
            "entity": [1],
            "feature": 2
          },
          "addressDestination": {
            "device": "Kermi-EEBUS-Demo-Client",
            "entity": [1],
            "feature": 2
          },
          "msgCounter": 15,
          "cmdClassifier": "write",
          "ackRequest": true
        },
        "payload": {
          "cmd": [
            {
              "function": "loadControlLimitListData",
              "filter": [
                {
                  "cmdControl": {
                    "partial": {}
                  }
                }
              ],
              "loadControlLimitListData": {
                "loadControlLimitData": [
                  {
                    "limitId": 0,
                    "isLimitActive": false,
                    "value": {
                      "number": 0,
                      "scale": 0
                    }
                  }
                ]
              }
            }
          ]
        }
      }
    }
  },
  "extension": null
}
"""; public static string MsgLoadControlLimitListDataNotify = """
{
  "data": {
    "header": {
      "protocolId": "ee1.0"
    },
    "payload": {
      "datagram": {
        "header": {
          "specificationVersion": "1.3.0",
          "addressSource": {
            "device": "Kermi-EEBUS-Demo-Client",
            "entity": [1],
            "feature": 2
          },
          "addressDestination": {
            "device": "d:_n:eebus-tester-tui_eebus-tester-tui-123456",
            "entity": [1],
            "feature": 2
          },
          "msgCounter": 15,
          "cmdClassifier": "notify"
        },
        "payload": {
          "cmd": [
            {
              "loadControlLimitListData": {
                "loadControlLimitData": [
                  {
                    "limitId": 0,
                    "isLimitChangeable": true,
                    "isLimitActive": false,
                    "timePeriod": {
                      "startTime": null,
                      "endTime": null
                    },
                    "value": {
                      "number": 0,
                      "scale": 0
                    }
                  }
                ]
              }
            }
          ]
        }
      }
    }
  },
  "extension": null
}
""";


    //public static class KermiLpcMessages
    //{
        // --- LoadControl ---

        public static string LoadControl_Write_DeleteTimePeriod_AndUpdate = """
    {"data":[{"header":[{"protocolId":"ee1.0"}]},{"payload":{"datagram":[{"header":[{"specificationVersion":"1.4.0"},{"addressSource":[{"device":"d:_i:002518F13E65_PPC"},{"entity":[1]},{"feature":2}]},{"addressDestination":[{"device":"Kermi-EEBUS-Demo-Client"},{"entity":[1]},{"feature":2}]},{"msgCounter":94},{"cmdClassifier":"write"},{"ackRequest":true}]},{"payload":[{"cmd":[[{"function":"loadControlLimitListData"},{"filter":[[{"cmdControl":[{"delete":[]}]},{"loadControlLimitListDataSelectors":[{"limitId":0}]},{"loadControlLimitDataElements":[{"timePeriod":[]}]}],[{"cmdControl":[{"partial":[]}]}]]},{"loadControlLimitListData":[{"loadControlLimitData":[[{"limitId":0},{"isLimitActive":false},{"value":[{"number":4884},{"scale":0}]}]]}]}]]}]}]}}]}
    """;

        public static string LoadControl_Notify_WithPartial = """
    {"data":[{"header":[{"protocolId":"ee1.0"}]},{"payload":{"datagram":[{"header":[{"specificationVersion":"1.3.0"},{"addressSource":[{"device":"Kermi-EEBUS-Demo-Client"},{"entity":[1]},{"feature":2}]},{"addressDestination":[{"device":"d:_i:002518F13E65_PPC"},{"entity":[4]},{"feature":1}]},{"msgCounter":30536},{"cmdClassifier":"notify"}]},{"payload":[{"cmd":[[{"function":"loadControlLimitListData"},{"filter":[[{"cmdControl":[{"partial":[]}]}]]},{"loadControlLimitListData":[{"loadControlLimitData":[[{"limitId":0},{"isLimitActive":false},{"value":[{"number":4884},{"scale":0}]}]]}]}]]}]}]}}]}
    """;

        public static string LoadControl_Notify_WithoutPartial = """
    {"data":[{"header":[{"protocolId":"ee1.0"}]},{"payload":{"datagram":[{"header":[{"specificationVersion":"1.3.0"},{"addressSource":[{"device":"Kermi-EEBUS-Demo-Client"},{"entity":[1]},{"feature":2}]},{"addressDestination":[{"device":"d:_i:002518F13E65_PPC"},{"entity":[4]},{"feature":1}]},{"msgCounter":30536},{"cmdClassifier":"notify"}]},{"payload":[{"cmd":[[{"loadControlLimitListData":[{"loadControlLimitData":[[{"limitId":0},{"isLimitChangeable":true},{"isLimitActive":false},{"value":[{"number":4884},{"scale":0}]}]]}]}]]}]}]}}]}
    """;

        public static string LoadControl_Result_ForMsgCounter94 = """
    {"data":[{"header":[{"protocolId":"ee1.0"}]},{"payload":{"datagram":[{"header":[{"specificationVersion":"1.3.0"},{"addressSource":[{"device":"Kermi-EEBUS-Demo-Client"},{"entity":[1]},{"feature":2}]},{"addressDestination":[{"device":"d:_i:002518F13E65_PPC"},{"entity":[4]},{"feature":1}]},{"msgCounter":30537},{"msgCounterReference":94},{"cmdClassifier":"result"}]},{"payload":[{"cmd":[[{"resultData":[{"errorNumber":0}]}]]}]}]}}]}
    """;

        public static string LoadControl_Write_UpdateOnly = """
    {"data":[{"header":[{"protocolId":"ee1.0"}]},{"payload":{"datagram":[{"header":[{"specificationVersion":"1.4.0"},{"addressSource":[{"device":"d:_i:002518F13E65_PPC"},{"entity":[4]},{"feature":1}]},{"addressDestination":[{"device":"Kermi-EEBUS-Demo-Client"},{"entity":[1]},{"feature":2}]},{"msgCounter":94},{"cmdClassifier":"write"},{"ackRequest":true}]},{"payload":[{"cmd":[[{"function":"loadControlLimitListData"},{"filter":[[{"cmdControl":[{"partial":[]}]}]]},{"loadControlLimitListData":[{"loadControlLimitData":[[{"limitId":0},{"isLimitActive":false},{"value":[{"number":4884},{"scale":0}]}]]}]}]]}]}]}}]}
    """;


        // --- deviceConfiguration ---

        public static string DeviceConfiguration_Write_FailsafeValue = """
    {"data":[{"header":[{"protocolId":"ee1.0"}]},{"payload":{"datagram":[{"header":[{"specificationVersion":"1.3.0"},{"addressSource":[{"device":"d:_n:Controlbox"},{"entity":[2]},{"feature":3}]},{"addressDestination":[{"device":"Kermi-EEBUS-Demo-Client"},{"entity":[1]},{"feature":3}]},{"msgCounter":44},{"cmdClassifier":"write"},{"ackRequest":true}]},{"payload":[{"cmd":[[{"function":"deviceConfigurationKeyValueListData"},{"filter":[[{"cmdControl":[{"partial":[]}]}]]},{"deviceConfigurationKeyValueListData":[{"deviceConfigurationKeyValueData":[[{"keyId":0},{"value":[{"scaledNumber":[{"number":4140},{"scale":0}]}]}]]}]}]]}]}]}}]}
    """;

        public static string DeviceConfiguration_Notify_FailsafeValueAndDuration = """
    {"data":[{"header":[{"protocolId":"ee1.0"}]},{"payload":{"datagram":[{"header":[{"specificationVersion":"1.3.0"},{"addressSource":[{"device":"Kermi-EEBUS-Demo-Client"},{"entity":[1]},{"feature":3}]},{"addressDestination":[{"device":"d:_n:Controlbox"},{"entity":[2]},{"feature":3}]},{"msgCounter":196},{"cmdClassifier":"notify"}]},{"payload":[{"cmd":[[{"deviceConfigurationKeyValueListData":[{"deviceConfigurationKeyValueData":[[{"keyId":0},{"value":[{"scaledNumber":[{"number":4140},{"scale":0}]}]},{"isValueChangeable":true}],[{"keyId":1},{"value":[{"duration":"P0D"}]},{"isValueChangeable":true}]]}]}]]}]}]}}]}
    """;

        public static string DeviceConfiguration_Result_ForMsgCounter44 = """
    {"data":[{"header":[{"protocolId":"ee1.0"}]},{"payload":{"datagram":[{"header":[{"specificationVersion":"1.3.0"},{"addressSource":[{"device":"Kermi-EEBUS-Demo-Client"},{"entity":[1]},{"feature":3}]},{"addressDestination":[{"device":"d:_n:Controlbox"},{"entity":[2]},{"feature":3}]},{"msgCounter":197},{"msgCounterReference":44},{"cmdClassifier":"result"}]},{"payload":[{"cmd":[[{"resultData":[{"errorNumber":0}]}]]}]}]}}]}
    """;

        public static string DeviceConfiguration_Write_MinimumFailsafeDuration = """
    {"data":[{"header":[{"protocolId":"ee1.0"}]},{"payload":{"datagram":[{"header":[{"specificationVersion":"1.3.0"},{"addressSource":[{"device":"d:_n:Controlbox"},{"entity":[2]},{"feature":3}]},{"addressDestination":[{"device":"Kermi-EEBUS-Demo-Client"},{"entity":[1]},{"feature":3}]},{"msgCounter":201},{"cmdClassifier":"write"},{"ackRequest":true}]},{"payload":[{"cmd":[[{"function":"deviceConfigurationKeyValueListData"},{"filter":[[{"cmdControl":[{"partial":[]}]}]]},{"deviceConfigurationKeyValueListData":[{"deviceConfigurationKeyValueData":[[{"keyId":1},{"value":[{"duration":"PT2H"}]}]]}]}]]}]}]}}]}
    """;

        public static string DeviceConfiguration_Notify_MinimumFailsafeDuration = """
    {"data":[{"header":[{"protocolId":"ee1.0"}]},{"payload":{"datagram":[{"header":[{"specificationVersion":"1.3.0"},{"addressSource":[{"device":"Kermi-EEBUS-Demo-Client"},{"entity":[1]},{"feature":3}]},{"addressDestination":[{"device":"d:_n:Controlbox"},{"entity":[2]},{"feature":3}]},{"msgCounter":2141},{"cmdClassifier":"notify"}]},{"payload":[{"cmd":[[{"deviceConfigurationKeyValueListData":[{"deviceConfigurationKeyValueData":[[{"keyId":0},{"value":[{"scaledNumber":[{"number":4140},{"scale":0}]}]},{"isValueChangeable":true}],[{"keyId":1},{"value":[{"duration":"PT2H"}]},{"isValueChangeable":true}]]}]}]]}]}]}}]}
    """;

        public static string DeviceConfiguration_Result_ForMsgCounter201 = """
    {"data":[{"header":[{"protocolId":"ee1.0"}]},{"payload":{"datagram":[{"header":[{"specificationVersion":"1.3.0"},{"addressSource":[{"device":"Kermi-EEBUS-Demo-Client"},{"entity":[1]},{"feature":3}]},{"addressDestination":[{"device":"d:_n:Controlbox"},{"entity":[2]},{"feature":3}]},{"msgCounter":2142},{"msgCounterReference":201},{"cmdClassifier":"result"}]},{"payload":[{"cmd":[[{"resultData":[{"errorNumber":0}]}]]}]}]}}]}
    """;
   // }
}


