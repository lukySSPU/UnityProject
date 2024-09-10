using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class PickupObjects : MonoBehaviour
{
    [SerializeField]
    private GameObject heldObject;
    private Transform heldObjectTF;
    private Rigidbody heldObjectRB;
    [SerializeField]
    private LayerMask heldObjectLayer;
    [SerializeField]
    private LayerMask pickUpLayer;
    [SerializeField]
    private LayerMask wallLayer;

    private Vector3 left;
    private Vector3 right;
    private Vector3 top;
    private Vector3 bottom;

    private float orgDistanceToScaleRatio;
    private Vector3 orgViewportPos;

    private List<Vector3> shapedGrid = new List<Vector3>();

    private int NUMBER_OF_GRID_ROWS = 10;
    private int NUMBER_OF_GRID_COLUMNS = 10;
    private const float SCALE_MARGIN = 0.001f;

    private void FixedUpdate()
    {
        if (heldObject == null) return;

        MoveInFrontOfObstacles();

        UpdateScale();
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            PickUpObject();
        }
    }

    void MoveInFrontOfObstacles()
    {
        Debug.Log("MoveInFrontOfObstacles()");
        if (shapedGrid.Count == 0) throw new System.Exception("Shaped grid calculation error");

        float closestZ = 1000;
        for (int i = 0; i < shapedGrid.Count; i++)
        {
            RaycastHit hit = CastTowardsGridPoint(shapedGrid[i], wallLayer + pickUpLayer);
            if (hit.collider == null) continue;

            Vector3 wallPoint = transform.InverseTransformPoint(hit.point);
            if (i == 0 || wallPoint.z < closestZ)
            {
                closestZ = wallPoint.z;
            }
        }

        float boundsMagnitude = heldObject.GetComponent<Renderer>().localBounds.extents.magnitude * heldObjectTF.localScale.x;
        Vector3 newLocalPos = heldObjectTF.localPosition;
        newLocalPos.z = closestZ - boundsMagnitude;
        heldObjectTF.localPosition = newLocalPos;
    }

    void UpdateScale()
    {
        Debug.Log("UpdateScale()");
        float newScale = (transform.position - heldObjectTF.position).magnitude / orgDistanceToScaleRatio;
        if (Mathf.Abs(newScale - heldObjectTF.localScale.x) < SCALE_MARGIN) return;

        heldObjectTF.localScale = new Vector3(newScale, newScale, newScale);

        Vector3 newPos = Camera.main.ViewportToWorldPoint(new Vector3(orgViewportPos.x, orgViewportPos.y, (heldObjectTF.position - transform.position).magnitude));
        heldObjectTF.position = newPos;
    }

    //picks up object
    void PickUpObject()
    {
        if (heldObject != null)
        {
            heldObjectTF.parent = null;
            heldObjectRB.useGravity = true;
            heldObjectRB.constraints = RigidbodyConstraints.None;
            heldObject.layer = (int)Mathf.Log(pickUpLayer.value, 2);
            heldObject = null;
            return;
        }

        RaycastHit hit;
        Physics.Raycast(transform.position, transform.forward, out hit, 100, pickUpLayer);

        if (hit.collider == null) return;

        heldObject = hit.collider.gameObject;
        heldObjectTF = heldObject.transform;
        heldObjectRB = heldObject.GetComponent<Rigidbody>();

        float scale = heldObjectTF.localScale.x;
        if (Mathf.Abs(scale - heldObjectTF.localScale.y) > SCALE_MARGIN
            || Mathf.Abs(scale - heldObjectTF.localScale.z) > SCALE_MARGIN)
            throw new System.Exception("Wrong object scale!");
        orgDistanceToScaleRatio = (transform.position - heldObjectTF.position).magnitude / scale;

        heldObject.layer = (int)Mathf.Log(heldObjectLayer.value, 2);
        heldObjectRB.useGravity = false;
        heldObjectTF.parent = transform;
        orgViewportPos = Camera.main.WorldToViewportPoint(heldObjectTF.position);
        heldObjectRB.constraints = RigidbodyConstraints.FreezeAll;

        Vector3[] bbPoints = GetBoundingBoxPoints();
        SetupShapedGrid(bbPoints);
    }

    private Vector3[] GetBoundingBoxPoints()
    {
        Debug.Log("GetBoundingBoxPoints()");
        Vector3 size = heldObject.GetComponent<Renderer>().localBounds.size;
        Vector3 x = new Vector3(size.x, 0, 0);
        Vector3 y = new Vector3(0, size.y, 0);
        Vector3 z = new Vector3(0, 0, size.z);
        Vector3 min = heldObject.GetComponent<Renderer>().localBounds.min;
        Vector3[] bbPoints =
        {
            min,
            min + x,
            min + y,
            min + x + y,
            min + z,
            min + z + x,
            min + z + y,
            min + z + x + y
        };
        return bbPoints;
    }

    private void SetupShapedGrid(Vector3[] bbPoints)
    {
        Debug.Log("SetupShapedGrid()");
        left = right = top = bottom = Vector2.zero;
        GetRectConfines(bbPoints);

        Vector3[,] grid = SetupGrid();
        GetShapedGrid(grid);
    }

    private void GetRectConfines(Vector3[] bbPoints)
    {
        Debug.Log("GetRectConfines()");
        Vector3 bbPoint;
        Vector3 cameraPoint;
        Vector2 viewportPoint;
        Vector3 closestPoint = heldObject.GetComponent<Renderer>().localBounds.ClosestPoint(transform.position);
        float closestZ = transform.InverseTransformPoint(heldObjectTF.TransformPoint(closestPoint)).z;
        if (closestZ <= 0) throw new System.Exception("HeldObject's inside the player!");

        for (int i = 0; i < bbPoints.Length; i++)
        {
            bbPoint = heldObjectTF.TransformPoint(bbPoints[i]);
            viewportPoint = Camera.main.WorldToViewportPoint(bbPoint);
            cameraPoint = transform.InverseTransformPoint(bbPoint);
            cameraPoint.z = closestZ;

            if (viewportPoint.x < 0 || viewportPoint.x > 1 || viewportPoint.y < 0 || viewportPoint.y > 1) continue;

            if (i == 0) left = right = top = bottom = cameraPoint;

            if (cameraPoint.x < left.x) left = cameraPoint;
            if (cameraPoint.x > right.x) right = cameraPoint;
            if (cameraPoint.y > top.y) top = cameraPoint;
            if (cameraPoint.y < bottom.y) bottom = cameraPoint;
        }
    }

    private Vector3[,] SetupGrid()
    {
        Debug.Log("SetupGrid()");
        float rectHrLength = right.x - left.x;
        float rectVertLength = top.y - bottom.y;
        Vector3 hrStep = new Vector2(rectHrLength / (NUMBER_OF_GRID_COLUMNS - 1), 0);
        Vector3 vertStep = new Vector2(0, rectVertLength / (NUMBER_OF_GRID_ROWS - 1));

        Vector3[,] grid = new Vector3[NUMBER_OF_GRID_ROWS, NUMBER_OF_GRID_COLUMNS];
        grid[0, 0] = new Vector3(left.x, bottom.y, left.z);

        for (int i = 0; i < grid.GetLength(0); i++)
        {
            for (int w = 0; w < grid.GetLength(1); w++)
            {
                if (i == 0 & w == 0) continue;
                else if (w == 0)
                {
                    grid[i, w] = grid[i - 1, 0] + vertStep;
                }
                else grid[i, w] = grid[i, w - 1] + hrStep;
            }
        }
        return grid;
    }

    private void GetShapedGrid(Vector3[,] grid)
    {
        Debug.Log("GetShapedGrid()");
        shapedGrid.Clear();
        foreach (Vector3 point in grid)
        {
            RaycastHit hit = CastTowardsGridPoint(point, heldObjectLayer);
            if (hit.collider != null) shapedGrid.Add(point);
        }
    }

    private RaycastHit CastTowardsGridPoint(Vector3 gridPoint, LayerMask layers)
    {
        Debug.Log("CastTowardsGridPoint()");
        Vector3 worldPoint = transform.TransformPoint(gridPoint);
        Vector3 origin = Camera.main.WorldToViewportPoint(worldPoint);
        origin.z = 0;
        origin = Camera.main.ViewportToWorldPoint(origin);
        Vector3 direction = worldPoint - origin;
        RaycastHit hit;
        Physics.Raycast(origin, direction, out hit, 1000, layers);
        return hit;
    }
}
