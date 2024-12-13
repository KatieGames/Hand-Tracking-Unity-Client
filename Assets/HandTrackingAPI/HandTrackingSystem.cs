namespace HandTrackingModule
{
    using System;
    using System.Collections.Generic;
    using UnityEngine;
    using HandTrackingModule.Websocket;
 
    public enum Gesture
    {
        FuckYou,
        ThumbsUp,
        Fist,
        OpenPalm,
        Ok,
        Metal,
        WebShooter,
        ErmAckshually,
        Victory,
        Number3,
        LmaoGottem
    }

    public enum Direction
    {
        Up,
        Down,
        Left,
        Right
    }

    public enum HandType
    {
        Right,
        Left
    }

    public enum ReceiveType
    {
        Landmarks,
        Gesture,
        Direction
    }

    public class DataReceivedEventArgs : EventArgs
    {
        public bool Success { get; set; }
    }

    // custom exception for api data requests
    public class ReceiveTypeException : Exception
    {
        public override string Message { get; }
        public ReceiveTypeException(ReceiveType type) { Message = $"Not receiving data of type: {type}"; }
    }

    public interface IHandTracking
    {
        event EventHandler<DataReceivedEventArgs> DataReceivedEvent;
        void Activate();
        void SetReceiveTypes(ReceiveType a);
        void SetReceiveTypes(ReceiveType a, ReceiveType b);
        void SetReceiveTypes(ReceiveType a, ReceiveType c, ReceiveType d);
        Vector3 GetLandmark(string name, HandType hand);
        Vector3 GetLandmark(int i, HandType hand);
        Gesture GetGesture(HandType hand);
        Direction GetDirection(HandType hand);
    }


    class HandTrackingSystem : MonoBehaviour, IHandTracking
    {
        private WebsocketListener WSListener = new();
        private Dictionary<ReceiveType, bool> ReceiveTypes = new() 
        {
            { ReceiveType.Direction, false },
            { ReceiveType.Landmarks, false },
            { ReceiveType.Gesture, false }
        };

        // data for each hand
        private Dictionary<HandType, HandData> HandsData = new()
        {
            { HandType.Right, new(HandType.Right) },
            { HandType.Left, new(HandType.Left)}
        };

        private bool ConnectionSuccessfull = false;

        /// <summary>
        /// Event Delegates 
        /// EventHandler Delegate > https://learn.microsoft.com/en-us/dotnet/api/system.eventhandler?view=net-9.0
        /// Handling and Raising Events > https://learn.microsoft.com/en-us/dotnet/standard/events/
        /// </summary>
        // Event is used to notify eventManager when hand data is received
        public event EventHandler<DataReceivedEventArgs> DataReceivedEvent;

        private void Start()
        {
            // DataReceived() will be called when WSListener receives data
            WSListener.DataReceivedDel += DataReceived;
        }

        private void Update()
        {
            /// <summary>
            /// string testJson = "{\"point0\": {\"x\": 0.36451587080955505, \"y\": 0.7492399215698242, \"z\": -1.5179728585223984e-09}, \"point1\": {\"x\": 0.34811195731163025, \"y\": 0.7084940671920776, \"z\": -0.05889080837368965}, \"point2\": {\"x\": 0.3175491392612457, \"y\": 0.6842957139015198, \"z\": -0.08604178577661514}, \"point3\": {\"x\": 0.28366610407829285, \"y\": 0.6672555804252625, \"z\": -0.09910319745540619}, \"point4\": {\"x\": 0.24972884356975555, \"y\": 0.6543729305267334, \"z\": -0.10958848148584366}, \"point5\": {\"x\": 0.26708173751831055, \"y\": 0.7825995683670044, \"z\": -0.07823742181062698}, \"point6\": {\"x\": 0.20796909928321838, \"y\": 0.7588865160942078, \"z\": -0.09051723033189774}, \"point7\": {\"x\": 0.18948495388031006, \"y\": 0.7131353616714478, \"z\": -0.09657265245914459}, \"point8\": {\"x\": 0.18492534756660461, \"y\": 0.6728100180625916, \"z\": -0.09973837435245514}, \"point9\": {\"x\": 0.2606772780418396, \"y\": 0.8008275628089905, \"z\": -0.0475996658205986}, \"point10\": {\"x\": 0.20537029206752777, \"y\": 0.7662889361381531, \"z\": -0.05684434249997139}, \"point11\": {\"x\": 0.18718063831329346, \"y\": 0.7139981985092163, \"z\": -0.06536299735307693}, \"point12\": {\"x\": 0.18383654952049255, \"y\": 0.6727067828178406, \"z\": -0.07237333804368973}, \"point13\": {\"x\": 0.25679683685302734, \"y\": 0.8021751046180725, \"z\": -0.017644982784986496}, \"point14\": {\"x\": 0.20753738284111023, \"y\": 0.772013247013092, \"z\": -0.021955737844109535}, \"point15\": {\"x\": 0.19017675518989563, \"y\": 0.7260228991508484, \"z\": -0.03151095658540726}, \"point16\": {\"x\": 0.1863010674715042, \"y\": 0.686863899230957, \"z\": -0.03952827677130699}, \"point17\": {\"x\": 0.2558435797691345, \"y\": 0.7944516539573669, \"z\": 0.00818195752799511}, \"point18\": {\"x\": 0.2147524505853653, \"y\": 0.7713050246238708, \"z\": 0.003776286030188203}, \"point19\": {\"x\": 0.19767005741596222, \"y\": 0.7385501265525818, \"z\": -0.003902278607711196}, \"point20\": {\"x\": 0.1897059679031372, \"y\": 0.7055431008338928, \"z\": -0.009910108521580696}}"; 
            /// DataReceived(testJson);
            /// </summary>

            // prevents ReceiveData call before connection has been established
            if (ConnectionSuccessfull)
            {
                // ReceiveData() uses semaphores for thread control, so does not need to be awaited
                WSListener.ReceiveData();
            }
        }

        private async void OnApplicationQuit()
        {
            WSRC status = await WSListener.CloseSocket();
            Debug.Log($"Closed websocket connection: {status}");
        }

        private void DataReceived(string json)
        {
            // splitting received string into array of substrings
            string[] jsonStrings = json.Trim('[', ']').Split("], [");

            // currently prefers right hand to left if there is only one set of hand points
            switch (jsonStrings.Length)
            {
                case 2:
                    HandsData[HandType.Left].SetFromJson(jsonStrings[1]);
                    goto case 1;
                case 1:
                    HandsData[HandType.Right].SetFromJson(jsonStrings[0]);
                    break;
            }

            DataReceivedEventArgs args = new()
            {
                Success = true
            };
            DataReceivedEvent?.Invoke(this, args);
        }

        private string GenerateTypeRequestCode()
        {
            char[] code = new char[3];
            int i = 0;
            foreach (bool value in ReceiveTypes.Values)
            {
                code[i] = value ? '1' : '0';
                i++;
            }
            // the code is a compact representation of what modes are active
            // e.g. 010 -> only points are active | 101 -> direction and gestures are active
            return new string(code);
        }
        public async void Activate()
        {
            try
            {
                if (!ReceiveTypes.ContainsValue(true))
                {
                    throw new Exception("No receive mode set, websocket cannot activate");
                }
                {
                    WSRC status = await WSListener.OpenSocket();
                    Debug.Log($"Websocket connection: {status}");
                }
                {
                    WSRC status = await WSListener.SendModeRequest(GenerateTypeRequestCode());
                    Debug.Log($"Websocket mode request sent: {status}");
                    if (status == WSRC.Success)
                    {
                        ConnectionSuccessfull = true;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError(e);
            }
        }

        // method overloading so SetReceiveTypes can be called with 1-3 modes in any order
        public void SetReceiveTypes(ReceiveType a) { ReceiveTypes[a] = true; }
        public void SetReceiveTypes(ReceiveType a, ReceiveType b) { ReceiveTypes[a] = true; ReceiveTypes[b] = true; }
        public void SetReceiveTypes(ReceiveType a, ReceiveType b, ReceiveType c) { ReceiveTypes[a] = true; ReceiveTypes[b] = true; ReceiveTypes[c] = true; }


        private bool ReceiveTypeValidation(ReceiveType type)
        {
            try
            {
                if (!ReceiveTypes[type])
                {
                    throw new ReceiveTypeException(type);
                }
                return true;
            }
            catch (ReceiveTypeException e)
            {
                Debug.LogError(e);
                return false;
            }
        }

        public Vector3 GetLandmark(string name, HandType hand = HandType.Right)
        {
            if (ReceiveTypeValidation(ReceiveType.Landmarks))
            {
                return HandsData[hand].GetPoint(name);
            }
            return default;
        }
        // method overloading so GetLandmark() can be called with an index instead of a string
        public Vector3 GetLandmark(int index, HandType hand = HandType.Right)
        {
            if (ReceiveTypeValidation(ReceiveType.Landmarks))
            {
                return HandsData[hand].GetPoint($"point{index}");
            }
            return default;
        }

        public Gesture GetGesture(HandType hand = HandType.Right)
        {
            if (ReceiveTypeValidation(ReceiveType.Gesture))
            {
                return HandsData[hand].Gesture;
            }
            return default;
        }

        public Direction GetDirection(HandType hand = HandType.Right)
        {
            if (ReceiveTypeValidation(ReceiveType.Direction))
            {
                return HandsData[hand].Direction;
            }
            return default;
        }
    }

    [Serializable]
    struct HandPoints
    {
        public Vector3 point0;
        public Vector3 point1;
        public Vector3 point2;
        public Vector3 point3;
        public Vector3 point4;
        public Vector3 point5;
        public Vector3 point6;
        public Vector3 point7;
        public Vector3 point8;
        public Vector3 point9;
        public Vector3 point10;
        public Vector3 point11;
        public Vector3 point12;
        public Vector3 point13;
        public Vector3 point14;
        public Vector3 point15;
        public Vector3 point16;
        public Vector3 point17;
        public Vector3 point18;
        public Vector3 point19;
        public Vector3 point20;

        public void PointsFromJson(string json)
        {
            // Debug.Log(json);
            this = JsonUtility.FromJson<HandPoints>(json);
        }

        /// <summary>
        /// C# Iterators 
        /// https://learn.microsoft.com/en-us/dotnet/csharp/iterators
        /// </summary>
        // allows iteration over the struct
        public IEnumerable<Vector3> GetPoints()
        {
            yield return point0;
            yield return point1;
            yield return point2;
            yield return point3;
            yield return point4;
            yield return point5;
            yield return point6;
            yield return point7;
            yield return point8;
            yield return point9;
            yield return point10;
            yield return point11;
            yield return point12;
            yield return point13;
            yield return point14;
            yield return point15;
            yield return point16;
            yield return point17;
            yield return point18;
            yield return point19;
            yield return point20;
        }

    }

    class HandData
    {
        // temp structure while refactoring
        public HandPoints handPoints;

        private Dictionary<string, Vector3> Landmarks = new();
        public HandType Hand { get; }
        public Gesture Gesture { get; private set; }
        public Direction Direction {  get; private set; }

        public HandData(HandType hand) { Hand = hand; }

        public Vector3 GetPoint(string key)
        {
            // only returns value if it is contained by the dictionary
            try
            {
                if (!Landmarks.ContainsKey(key))
                {
                    throw new Exception($"{key} is not a valid landmark");
                }
                return Landmarks[key];
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return default;
            }
        }

        public void SetFromJson(string json)
        {
            handPoints.PointsFromJson(json);
            int n = 0;
            foreach (Vector3 point in handPoints.GetPoints())
            {
                string key = $"point{n}";
                // adds the key-value pair if key isnt already present, otherwise sets the value
                if (!Landmarks.TryAdd(key, point))
                {
                    Landmarks[key] = point;
                }
                n++;
            }
        }
    }
}