using System.Collections;
using System.Collections.Generic;
using OnePadSDK;
using UnityEngine;

public class BaseDynamicHandler : MonoBehaviour,IDynamicRequestHandler {
    public virtual string Request(string url, string query) {
        return null;
    }
}
