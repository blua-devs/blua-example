using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace bLua
{
    public class LuaException : Exception
    {
        public LuaException(string message) : base(message)
        {

        }
    }

    public static class bLuaError
    {
        public const string error_indexingUserdata = "error indexing userdata: ";
        public const string error_invalidTypeIndex = "invalid userdata type index: ";
        public const string error_invalidMethodIndex = "invalid userdata method index: ";
        public const string error_invalidPropertyIndex = "invalid userdata property index: ";
        public const string error_invalidFieldIndex = "invalid userdata field index: ";

        public const string error_invalidType = "invalid userdata type: ";
        public const string error_invalidMethod = "invalid userdata method: ";
        public const string error_invalidProperty = "invalid userdata property: ";
        public const string error_invalidField = "invalid userdata field: ";

        public const string error_callingFunction = "error calling function: ";
        public const string error_callingDelegate = "error calling delegate: ";
        public const string error_inFunctionCall = "error in function call: ";
        public const string error_setProperty = "failed to set property: ";

        public const string error_objectIsNotUserdata = "object is not userdata: ";
        public const string error_objectNotProvided = "object not provided when calling userdata: ";
        public const string error_invalidUserdata = "could not find valid userdata object";

        public const string error_stackIsEmpty = "stack is empty";
        public const string error_unrecognizedStackPush = "unrecognized object pushing onto stack: ";
    }
} // bLua namespace
