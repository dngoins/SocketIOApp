﻿/*
 *
Copyright 2018 Rodney Degracia

MIT License:

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.
*
*/

using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using UnityEngine.XR.MagicLeap;



using Prestige;


/*
 * Represents the behavior of the Cursor, which also has a LineRenderer
 * 
 * This script should be attached to the GameObject which represents the 
 * "end" of the picker "ray".
 * 
 * Works in conjunction with the InputController script, which should be 
 * attached to the GameObject that represents the Magic Leap controller
 * 
 * */

public class Cursor : MonoBehaviour
{
    [SerializeField]
    InputController inputController = null;

    public uint Width = 1;
    public uint Height = 1;
    public float HorizontalFovDegrees;
    public bool CollideWithUnobserved = false;
    public float defaultDistance = 9.0F;

    public delegate void CursorMove(Ray controllerRay, Transform cursorTransform, RaycastHit? raycast);
    public delegate void CursorHover(GameObject gameObject, Transform cursorTransform, RaycastHit raycastHit);
    public delegate void CursorStopHover(GameObject gameObject);

    public static event CursorMove OnCursorMove;
    public static event CursorHover OnCursorHover;
    public static event CursorStopHover OnCursorStopHover;

    private WorldRaysManager worldRaysManager = null;
    private Renderer _renderer;
    private LineRenderer lineRenderer;
    private Transform adjustedCursorTransform;
    private GameObject hoveredGameObject = null;

    protected Color color;
    protected bool scaleWhenClose = true;
    protected bool hit;

    protected WorldRaysManager GetWorldRaysManager()
    {
        return worldRaysManager;
    }

    protected LineRenderer GetLineRenderer()
    {
        return lineRenderer;
    }

    public GameObject GetHoveredGameObject()
    {
        return hoveredGameObject;
    }

    public Transform GetAdjustedCursorTransform()
    {
        return adjustedCursorTransform;
    }

    private void Awake()
    {
        Hashtable options = new Hashtable();

        Debug.Assert(inputController != null, "inputControllerBehavior should be configured in the Inspector");

        options["Width"] = Width;
        options["Height"] = Height;
        options["HorizontalFovDegrees"] = HorizontalFovDegrees;
        options["CollideWithUnobserved"] = CollideWithUnobserved;

        worldRaysManager = new WorldRaysManager(options);

        _renderer = GetComponent<Renderer>();

        lineRenderer = GetComponent<LineRenderer>();
        lineRenderer.startWidth = 0.01f;
        lineRenderer.endWidth = 0.01f;
    }

    void Start()
    {
        worldRaysManager.Start(WorldRaysCallback);

    }

    // Update is called once per frame
    void Update()
    {
        worldRaysManager.Update(inputController.transform.position, inputController.transform.forward, inputController.transform.up);
    }


    private void OnDestroy()
    {
        worldRaysManager.Stop();
    }

    virtual public void WorldRaysCallback(MLWorldRays.MLWorldRaycastResultState state, RaycastHit result, float confidence)
    {
        Vector3 rayCastOrigin = inputController.transform.position;
        Vector3 rayCastDirection = inputController.transform.forward;

        if (state != MLWorldRays.MLWorldRaycastResultState.RequestFailed && state != MLWorldRays.MLWorldRaycastResultState.NoCollision)
        {
            // Update the cursor position and normal.
            transform.position = result.point;
            transform.LookAt(result.normal + result.point);
            transform.localScale = Vector3.one;

            // Set the color to yellow if the hit is unobserved.
            _renderer.material.color = (state == MLWorldRays.MLWorldRaycastResultState.HitObserved) ? color : Color.yellow;

            if (scaleWhenClose)
            {
                // Check the hit distance.
                if (result.distance < 1.0f)
                {
                    // Apply a downward scale to the cursor.
                    transform.localScale = new Vector3(result.distance, result.distance, result.distance);
                }
            }

            hit = true;
        }
        else
        {
            // Update the cursor position and normal.
            transform.position = (rayCastOrigin + (rayCastDirection * defaultDistance));
            transform.LookAt(rayCastOrigin);
            transform.localScale = Vector3.one;

            _renderer.material.color = Color.red;

            hit = false;
        }

        lineRenderer.positionCount = 2;
        lineRenderer.SetPosition(0, rayCastOrigin);
        lineRenderer.SetPosition(1, transform.position);
        lineRenderer.useWorldSpace = true;

        bool hitWorldMesh = (hit == true);
        bool outsideWorldMesh = (hit == false);

        RaycastHit raycastHit;
        System.Int32 defaultLayer = 1;  //  0000 0001   
        bool didHitGameObject = (Physics.Raycast(rayCastOrigin, rayCastDirection, out raycastHit, Vector3.Distance(rayCastOrigin, transform.position), defaultLayer));
        Ray controllerRay = new Ray(rayCastOrigin, rayCastDirection);

        if (didHitGameObject)
        {
            Debug.Log("Hit GameObject");

            // Update the cursor position and normal.
            transform.position = raycastHit.point;
            transform.LookAt(raycastHit.normal + raycastHit.point);
            transform.localScale = Vector3.one;

            adjustedCursorTransform = transform;

            // Adjust ray to end at the inGamHit
            lineRenderer.SetPosition(1, raycastHit.point);

            // Set the color to yellow if the hit is unobserved.
            _renderer.material.color = (state == MLWorldRays.MLWorldRaycastResultState.HitObserved) ? color : Color.yellow;

            if (scaleWhenClose)
            {
                // Check the hit distance.
                if (raycastHit.distance < 1.0f)
                {
                    // Apply a downward scale to the cursor.
                    transform.localScale = new Vector3(raycastHit.distance, raycastHit.distance, raycastHit.distance);
                }
            }

            bool newObject = (hoveredGameObject == null);
            bool differentObject = (hoveredGameObject != null && raycastHit.collider.gameObject.GetInstanceID() != this.hoveredGameObject.GetInstanceID());

            if (newObject)
            {
                hoveredGameObject = raycastHit.collider.gameObject;
                OnCursorHover(hoveredGameObject, transform, raycastHit);
                lineRenderer.material.color = Color.green;
            }
            else if (differentObject)
            {
                OnCursorStopHover(hoveredGameObject);

                hoveredGameObject = raycastHit.collider.gameObject;

                OnCursorHover(hoveredGameObject, transform, raycastHit);

                lineRenderer.material.color = Color.green;

            }
            else
            {
                // Same Object
                ;   // do nothing
            }

            OnCursorMove(controllerRay, adjustedCursorTransform, raycastHit);


        }
        else
        {
            if (hitWorldMesh)
            {
                // Debug.Log("Hit world mesh");
                lineRenderer.material.color = Color.yellow;

                if (this.hoveredGameObject != null)
                {
                    OnCursorStopHover(hoveredGameObject);
                    hoveredGameObject = null;

                }

                if (OnCursorMove != null)
                {
                    OnCursorMove(controllerRay, transform, result);
                }

            }
            else if (outsideWorldMesh)
            {
                Debug.Log("Did not hit world mesh or game object");
                lineRenderer.material.color = Color.red;

                adjustedCursorTransform = transform;
                adjustedCursorTransform.position = rayCastOrigin + (rayCastDirection.normalized * 2);

                OnCursorMove(controllerRay, adjustedCursorTransform, null);


                if (this.hoveredGameObject != null)
                {
                    OnCursorStopHover(hoveredGameObject);
                    hoveredGameObject = null;


                }
            }
        }
    }
}
