using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Kinect = Windows.Kinect;

public class BodyView : MonoBehaviour
{
    public GameObject BodyManager;
    
    //We use a body prefab to generate the bodies of each tracked person
    public GameObject FullBodyPrefab;
    GameObject FullBody;

    private Dictionary<ulong, GameObject> _Bodies = new Dictionary<ulong, GameObject>();

    //This dictionary contains references of the bones of the instantiated body
    private Dictionary<ulong, Transform[]> _JointRig = new Dictionary<ulong, Transform[]>();
    //private List<GameObject> JointRig = new List<GameObject>();

    private BodyManager _BodyManager;
    
    // map out all the bones by the two joints that they will be connected to
    private Dictionary<Kinect.JointType, Kinect.JointType> _BoneMap = new Dictionary<Kinect.JointType, Kinect.JointType>()
    {
        //Left leg
        { Kinect.JointType.FootLeft, Kinect.JointType.AnkleLeft },
        { Kinect.JointType.AnkleLeft, Kinect.JointType.KneeLeft },
        { Kinect.JointType.KneeLeft, Kinect.JointType.HipLeft },
        { Kinect.JointType.HipLeft, Kinect.JointType.SpineBase },

        //Right leg
        { Kinect.JointType.FootRight, Kinect.JointType.AnkleRight },
        { Kinect.JointType.AnkleRight, Kinect.JointType.KneeRight },
        { Kinect.JointType.KneeRight, Kinect.JointType.HipRight },
        { Kinect.JointType.HipRight, Kinect.JointType.SpineBase },

        //Left arm
        { Kinect.JointType.HandTipLeft, Kinect.JointType.HandLeft }, //Need this for HandSates
        { Kinect.JointType.ThumbLeft, Kinect.JointType.HandLeft },
        { Kinect.JointType.HandLeft, Kinect.JointType.WristLeft },
        { Kinect.JointType.WristLeft, Kinect.JointType.ElbowLeft },
        { Kinect.JointType.ElbowLeft, Kinect.JointType.ShoulderLeft },
        { Kinect.JointType.ShoulderLeft, Kinect.JointType.SpineShoulder },

        //Right arm
        { Kinect.JointType.HandTipRight, Kinect.JointType.HandRight }, //Needthis for Hand State
        { Kinect.JointType.ThumbRight, Kinect.JointType.HandRight },
        { Kinect.JointType.HandRight, Kinect.JointType.WristRight },
        { Kinect.JointType.WristRight, Kinect.JointType.ElbowRight },
        { Kinect.JointType.ElbowRight, Kinect.JointType.ShoulderRight },
        { Kinect.JointType.ShoulderRight, Kinect.JointType.SpineShoulder },

        //Spine and head
        { Kinect.JointType.SpineBase, Kinect.JointType.SpineMid },
        { Kinect.JointType.SpineMid, Kinect.JointType.SpineShoulder },
        { Kinect.JointType.SpineShoulder, Kinect.JointType.Neck },
        { Kinect.JointType.Neck, Kinect.JointType.Head },
    };

    //This dictionary will be used to find all the bones of the armature
    private Dictionary<string, int> _KinectToRig = new Dictionary<string, int>()
    {
        //Left leg
        { "Foot_L", 15},
        { "Ankle_L", 14},
        { "Knee_L", 13},
        { "Hip_L", 12},

        //Right leg
        { "Foot_R", 19},
        { "Ankle_R", 18},
        {"Knee_R", 17},
        {"Hip_R", 16},

        //Left arm
        { "HandTip_L", 21}, //Need this for HandSates
        { "Thumb_L", 22},
        { "Hand_L", 7},
        { "Wrist_L", 6},
        { "Elbow_L", 5},
        { "Shoulder_L", 4},

        //Right arm
        { "HandTip_R", 23}, //Needthis for Hand State
        { "Thumb_R", 24},
        { "Hand_R", 11},
        { "Wrist_R", 10},
        { "Elbow_R", 9},
        { "Shoulder_R", 8},

        //Spine and head
        { "SpineBase", 0},
        { "SpineMid", 1},
        { "SpineShoulder", 20},
        { "Neck", 2},

        {"Head", 3}
    };
    // Update is called on each frames
    void Update()
    {
        //we need to store the keys of the tracked bodies in order to generate them
        //We check if the KinectManager has data to work with

        if (BodyManager == null)
        {
            return;
        }

        _BodyManager = BodyManager.GetComponent<BodyManager>();
        if (_BodyManager == null)
        {
            return;
        }
        //We store the data of the bodies detected
        Kinect.Body[] data = _BodyManager.GetData();
        if (data == null)
        {
            return;
        }

        /////////////////////////////////////////////////////////////////////////////////
        //List of the tracked bodies (ulong is a an unsigned integer)
        List<ulong> trackedIds = new List<ulong>();
        foreach (var body in data)
        {
            if (body == null)
            {
                continue;
            }

            if (body.IsTracked)
            {
                trackedIds.Add(body.TrackingId);  //We add the ID of all the tracked body from the the current frame in the tracked body list

                if (!_Bodies.ContainsKey(body.TrackingId))
                {
                    //if the body isn't already in the _Bodies dictionnary, we create a new body object and add it to the dictionnary
                    _Bodies[body.TrackingId] = CreateBodyObject(body.TrackingId, body);
                    //We need to associate the joints of the specific character to his body
                    _JointRig[body.TrackingId] = ReferenceRigToBody(body, _KinectToRig, _Bodies[body.TrackingId]);
                        
                }
                //otherwise the body exists already and we have to refresh it
                RefreshBodyObject(body, _Bodies[body.TrackingId], _JointRig[body.TrackingId]);
            }
        }

        List<ulong> knownIds = new List<ulong>(_Bodies.Keys);

        // First delete untracked bodies
        foreach (ulong trackingId in knownIds)
        {
            if (!trackedIds.Contains(trackingId))   //we check every Ids in knownIds and check if the body are still tracked. If it isn't the case we destroy the body
            {                                       //by updating the _Bodies dictionnary
                Destroy(_Bodies[trackingId]);
                _Bodies.Remove(trackingId);
            }
        }
    }

    private GameObject CreateBodyObject(ulong id, Kinect.Body bodyKinect)
    {
        //We create a new body object named by the id
        FullBody = Instantiate(FullBodyPrefab) as GameObject;
        FullBody.name = "Body:" + id;

        //Joint hierarchy flows from the center of the body to the extremities. It will go on each joint
        //The value of SpineBase = 0 and the value of the ThumbRight = 24, the maximum 
        for (Kinect.JointType jt = Kinect.JointType.SpineBase; jt <= Kinect.JointType.ThumbRight; jt++)
        {
            GameObject jointObj = GameObject.CreatePrimitive(PrimitiveType.Sphere); //for each joint we create a gameObject with a sphere
            jointObj.GetComponent<Collider>().isTrigger = true;
            jointObj.transform.localScale = new Vector3(1f, 1f, 1f);
            jointObj.name = jt.ToString();
            jointObj.transform.parent = FullBody.transform; //we attach the joint to the body parent
        }

        return FullBody;
    }

    private void RefreshBodyObject(Kinect.Body body, GameObject bodyObject, Transform[] _JointRig) 
    {
        int RigIndex = 0;
        for (Kinect.JointType jt = Kinect.JointType.SpineBase; jt <= Kinect.JointType.ThumbRight; jt++)
        {
            Kinect.Joint sourceJoint = body.Joints[jt]; //we load the data of the kinect joint
            Debug.Log(_JointRig[RigIndex]);
            
            Transform jointObj = bodyObject.transform.FindChild(jt.ToString()); //we store the position of the current jt
            jointObj.localPosition = GetVector3FromJoint(sourceJoint); //set the local position of each joint, we "scale" the distance of everything by 10
                                                                       //otherwise we would be represented as a heap of spheres

            //we have to check if the current joint has another joint after
            if (_BoneMap.ContainsKey(jt) && jt != Kinect.JointType.Neck)
            {
                Kinect.Joint targetJoint = body.Joints[_BoneMap[jt]]; //parent of the analyzed joint
                //{ Kinect.JointType.FootLeft, Kinect.JointType.AnkleLeft }, AnkleLeft joint will be the targetJoint of FootLeft joint

                Transform targetJointObj = bodyObject.transform.FindChild(body.Joints[_BoneMap[jt]].JointType.ToString());

                jointObj.LookAt(targetJointObj);    //position the direction of the bones to point at their joint target
                Vector3 rot = jointObj.rotation.eulerAngles;
                rot = new Vector3(rot.x, rot.y, rot.z); //we have to add 90 degrees to the x axis of the bones
                jointObj.rotation = Quaternion.Euler(rot);              
                _JointRig[RigIndex].rotation = Quaternion.Euler(rot);             
                
            }
            RigIndex++;
        }
    }

    private Transform[] ReferenceRigToBody(Kinect.Body body, Dictionary<string, int> _KinectToRig, GameObject Fullbody)
    {
        List<Transform> JointRig = new List<Transform>();

        Transform[] Rig = new Transform[25];

        //this loop will go through all the bones of the body from the parameter
        var list = Fullbody.transform.GetChild(0).gameObject.GetComponentsInChildren<Transform>();
        foreach (var child in list)
        {
            if (!child.gameObject.name.Equals("Armature") && !child.gameObject.name.Equals("male-base") && !child.gameObject.name.Equals("male-highpolyeyes") && !child.gameObject.name.Contains("Body"))
            {
      
                Rig[_KinectToRig[child.gameObject.name]] = child.gameObject.transform;
                //Debug.Log(Rig[_KinectToRig[child.gameObject.name]] + " at index " + _KinectToRig[child.gameObject.name]);
            }
        }
        return Rig;
    }

    private static Vector3 GetVector3FromJoint(Kinect.Joint joint)
    {
        return new Vector3(joint.Position.X * 10, joint.Position.Y * 10, joint.Position.Z * 10);
    }

}
