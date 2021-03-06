﻿using Gma.System.MouseKeyHook;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using UnityEngine;

public class FollowMode : MonoBehaviour
{
    public Transform Cursor;
    public RouteHolder RouteHolder;

    public Material FollowMaterial;
    public Material HeartMaterial;

    public GameObject NodePrefab;
    public LineRenderer RouteDisplay;
    public LineRenderer OrientationHelper;

    public const float SquaredDistToReach = 1;
    public const float SquaredMaxRouteLength = 1000;

    public Route Route { get { return this.RouteHolder.Route; } }

    private int nodeIndex
    {
        get { return this.RouteHolder.NodeIndex; }
        set { this.RouteHolder.NodeIndex = value; }
    }

    private IKeyboardMouseEvents globalHook;

    private List<NodeDisplay> nodes = new List<NodeDisplay>();
    private List<NodeDisplay> detachedNodes = new List<NodeDisplay>();

    private void Awake()
    {
        this.globalHook = Hook.GlobalEvents();
    }

    private void OnEnable()
    {
        this.RepopulateRoute();
        this.OrientationHelper.gameObject.SetActive(false);
        this.globalHook.KeyDown += this.GlobalHookKeyDown;
    }

    private void OnDisable()
    {
        this.globalHook.KeyDown -= this.GlobalHookKeyDown;
    }

    private void OnDestroy()
    {
        this.globalHook.Dispose();
    }

    private void Update()
    {
        if (this.nodeIndex < 0)
            return;

        Node next = this.Route.Nodes[this.nodeIndex];

        this.OrientationHelper.SetPositions(new Vector3[] { this.Cursor.position, next.Position });

        if ((next.Position - this.Cursor.position).sqrMagnitude <= SquaredDistToReach)
            this.ReachedNode();
    }

    public void Reload()
    {
        this.nodes.ForEach(n => Destroy(n.gameObject));
        this.nodes.Clear();
        this.detachedNodes.ForEach(n => Destroy(n.gameObject));
        this.detachedNodes.Clear();
    }

    private void GlobalHookKeyDown(object sender, KeyEventArgs e)
    {
        if (Camera.main.cullingMask == 0)
            return;

        switch (e.KeyCode)
        {
            // Select closest connected node.
            case Keys.NumPad5:
                this.SelectClosestNode();
                break;

            // Manually change nodes.
            case Keys.NumPad4:
                if (this.nodeIndex > 0)
                {
                    this.nodeIndex--;
                    this.RepopulateRoute();
                }
                break;

            case Keys.NumPad6:
                this.ReachedNode();
                break;

            // Toggle the orientation helper.
            case Keys.NumPad0:
                this.OrientationHelper.gameObject.SetActive(!this.OrientationHelper.gameObject.activeSelf);
                break;
        }
    }

    private void RepopulateRoute()
    {
        // Repopulate attached nodes.
        this.nodes.ForEach(n => Destroy(n.gameObject));
        this.nodes.Clear();
        float squaredLength = 0;
        Node previous = null;
        foreach (Node node in this.Route.Nodes.Skip(this.nodeIndex))
        {
            GameObject gameObject = Instantiate(this.NodePrefab, this.RouteDisplay.transform);
            NodeDisplay display = gameObject.GetComponent<NodeDisplay>();
            display.Node = node;
            display.SetMesh(false);
            this.nodes.Add(display);

            if (previous == null)
                display.Select(true);

            if (node.Type == NodeType.Waypoint)
                break;

            if (previous != null)
            {
                squaredLength += (previous.Position - node.Position).sqrMagnitude;
                if (squaredLength > SquaredMaxRouteLength)
                    break;
            }

            previous = node;
        }

        // Update route display.
        this.RouteDisplay.positionCount = 0;  // TODO: Find out if this is needed.
        Vector3[] positions = this.nodes.Select(n => n.transform.position).ToArray();
        this.RouteDisplay.positionCount = positions.Length;
        this.RouteDisplay.SetPositions(positions);

        // Update route display material.
        this.RouteDisplay.material = this.FollowMaterial;
        foreach (Node node in this.Route.Nodes.Take(this.nodeIndex).Reverse())
        {
            if (node.Type == NodeType.HeartWall)
                break;

            if (node.Type == NodeType.Heart)
            {
                this.RouteDisplay.material = this.HeartMaterial;
                break;
            }
        }

        // Repopulate detached nodes.
        this.detachedNodes.ForEach(n => Destroy(n.gameObject));
        this.detachedNodes.Clear();
        foreach (Node node in this.Route.DetachedNodes)
        {
            GameObject gameObject = Instantiate(this.NodePrefab, this.RouteDisplay.transform);
            NodeDisplay display = gameObject.GetComponent<NodeDisplay>();
            display.Node = node;
            display.SetMesh(true);
            this.detachedNodes.Add(display);
        }
    }

    private void ReachedNode()
    {
        if (this.nodeIndex + 1 < this.Route.Nodes.Count)
        {
            Node reached = this.Route.Nodes[this.nodeIndex];
            if (reached.Type == NodeType.Waypoint && !string.IsNullOrEmpty(reached.WaypointCode))
                Clipboard.SetText(reached.WaypointCode);

            this.nodeIndex += 1;
            this.RepopulateRoute();
        }
    }

    private void SelectClosestNode()
    {
        if (this.nodes.Count != 0)
        {
            float d = -1;
            this.nodeIndex = 0;
            foreach (var item in this.Route.Nodes.Select((node, i) => new { position = node.Position, i = i }))
            {
                float cd = (this.Cursor.position - item.position).sqrMagnitude;
                if (d == -1 || d > cd)
                {
                    d = cd;
                    this.nodeIndex = item.i;
                }
            }
            this.RepopulateRoute();
        }
    }
}