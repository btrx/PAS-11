using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;

public class LevelGenerator : MonoBehaviour
{
    [Header("Spawner References")]
    [Tooltip("Reference to the enemy spawning system (optional)")]
    public EnemySpawner enemySpawner;

    [Tooltip("Reference to the collectible spawning system (optional)")]
    public CollectibleSpawner collectibleSpawner;

    [Header("Tilemap Setup")]
    [Tooltip("Grid prefab must contain 'Floor' and 'Wall' Tilemap children")]
    public GameObject gridPrefab;

    [Tooltip("Tile used for walkable floor areas")]
    public Tile floorTile;

    [Tooltip("Tile used for surrounding walls")]
    public Tile wallTile;

    private Tilemap floorTilemap;
    private Tilemap wallTilemap;

    [Header("Generation Settings")]
    [Range(50, 500)]
    [Tooltip("Number of steps the random walker takes (more steps = larger level)")]
    public int walkSteps = 200;

    [Tooltip("Starting position for the level generation walker")]
    public Vector2Int startPosition = Vector2Int.zero;

    [Range(50, 500)]
    [Tooltip("Minimum number of floor tiles required for a valid level")]
    public int minFloorTiles = 100;

    [Range(0, 3)]
    [Tooltip("Size of tile stamp: 0=single tile, 1=3x3 area, 2=5x5 area, 3=7x7 area")]
    public int stampSize = 1;

    [Range(10, 200)]
    [Tooltip("Maximum number of generation attempts before giving up")]
    public int maxGenerationAttempts = 100;

    void Start()
    {
        if (!ValidateSetup())
        {
            Debug.LogError("LevelGenerator setup is incomplete! Check the inspector for missing references.");
            return;
        }

        GenerateLevelWithRetries();
    }

    /// <summary>
    /// Validates that all required components are assigned
    /// </summary>
    private bool ValidateSetup()
    {
        bool isValid = true;

        if (gridPrefab == null)
        {
            Debug.LogError("Grid Prefab is not assigned in LevelGenerator!");
            isValid = false;
        }

        if (floorTile == null)
        {
            Debug.LogError("Floor Tile is not assigned in LevelGenerator!");
            isValid = false;
        }

        if (wallTile == null)
        {
            Debug.LogError("Wall Tile is not assigned in LevelGenerator!");
            isValid = false;
        }

        return isValid;
    }

    /// <summary>
    /// Attempts to generate a valid level, retrying if the level is too small
    /// </summary>
    void GenerateLevelWithRetries()
    {
        int attempts = 0;

        while (attempts < maxGenerationAttempts)
        {
            // Clear any previous generation
            if (floorTilemap != null)
            {
                Destroy(floorTilemap.transform.parent.gameObject); // Destroy the old Grid instance
            }

            // Instantiate a new Grid prefab
            GameObject gridInstance = Instantiate(gridPrefab, Vector3.zero, Quaternion.identity);
            floorTilemap = gridInstance.transform.Find("Floor").GetComponent<Tilemap>();
            wallTilemap = gridInstance.transform.Find("Wall").GetComponent<Tilemap>();

            if (floorTilemap == null || wallTilemap == null)
            {
                Debug.LogError("Could not find 'Floor' or 'Wall' Tilemaps in the instantiated Grid prefab! " +
                              "Make sure your Grid prefab has children named 'Floor' and 'Wall' with Tilemap components.");
                return; // Stop if prefab is not set up correctly
            }

            // Run generation and check conditions
            HashSet<Vector2Int> floorPositions = GenerateFloor();
            GenerateWalls(floorPositions);

            if (floorPositions.Count >= minFloorTiles)
            {
                Debug.Log($"Level generated successfully after {attempts + 1} attempt(s). Floor tiles: {floorPositions.Count}");

                // Spawn enemies if spawner is assigned
                if (enemySpawner != null)
                {
                    enemySpawner.SpawnEnemies(floorPositions, startPosition);
                }
                else
                {
                    Debug.LogWarning("EnemySpawner reference is missing! No enemies will be spawned.");
                }

                // Spawn collectibles if spawner is assigned
                if (collectibleSpawner != null)
                {
                    collectibleSpawner.SpawnCollectibles(floorPositions, startPosition);
                }
                else
                {
                    Debug.LogWarning("CollectibleSpawner reference is missing! No collectibles will be spawned.");
                }

                return; // Level is good, stop trying
            }
            else
            {
                Debug.Log($"Generated level too small ({floorPositions.Count} tiles). Retrying... (Attempt {attempts + 1}/{maxGenerationAttempts})");
                attempts++;
            }
        }

        Debug.LogError($"Failed to generate a valid level after {maxGenerationAttempts} attempts. " +
                      "Try increasing walkSteps or decreasing minFloorTiles.");
    }

    /// <summary>
    /// Generates floor tiles using a random walk algorithm
    /// </summary>
    /// <returns>Set of all floor tile positions</returns>
    private HashSet<Vector2Int> GenerateFloor()
    {
        Vector2Int currentPos = startPosition;
        HashSet<Vector2Int> floorPositions = new HashSet<Vector2Int>();

        for (int i = 0; i < walkSteps; i++)
        {
            // Place tiles in a 'stamp' around the current position
            // stampSize of 0 = 1x1 (just current tile)
            // stampSize of 1 = 3x3 (current + 1 tile in each direction)
            // stampSize of 2 = 5x5 (current + 2 tiles in each direction)
            for (int x = -stampSize; x <= stampSize; x++)
            {
                for (int y = -stampSize; y <= stampSize; y++)
                {
                    Vector2Int stampedPos = currentPos + new Vector2Int(x, y);
                    floorPositions.Add(stampedPos);
                    floorTilemap.SetTile((Vector3Int)stampedPos, floorTile);
                }
            }

            // Move in a random direction
            currentPos += GetRandomDirection();
        }

        return floorPositions;
    }

    /// <summary>
    /// Generates walls around all floor tiles
    /// </summary>
    /// <param name="floorPositions">Set of floor tile positions to surround with walls</param>
    private void GenerateWalls(HashSet<Vector2Int> floorPositions)
    {
        // Find all positions adjacent to floor tiles that don't have floor
        HashSet<Vector2Int> wallCandidatePositions = new HashSet<Vector2Int>();

        foreach (var position in floorPositions)
        {
            // Check all 8 directions (cardinal + diagonal) for empty spaces
            foreach (var direction in GetCardinalAndDiagonalDirections())
            {
                Vector2Int neighborPos = position + direction;

                // If this neighbor isn't a floor tile, it should be a wall
                if (!floorPositions.Contains(neighborPos))
                {
                    wallCandidatePositions.Add(neighborPos);
                }
            }
        }

        // Place wall tiles at all candidate positions
        foreach (var wallPos in wallCandidatePositions)
        {
            wallTilemap.SetTile((Vector3Int)wallPos, wallTile);
        }
    }

    /// <summary>
    /// Returns a random cardinal direction (up, down, left, right)
    /// </summary>
    private Vector2Int GetRandomDirection()
    {
        int choice = Random.Range(0, 4);
        switch (choice)
        {
            case 0: return Vector2Int.up;
            case 1: return Vector2Int.down;
            case 2: return Vector2Int.left;
            case 3: return Vector2Int.right;
            default: return Vector2Int.zero;
        }
    }

    /// <summary>
    /// Returns all 8 directions (cardinal + diagonal)
    /// </summary>
    private List<Vector2Int> GetCardinalAndDiagonalDirections()
    {
        return new List<Vector2Int>
        {
            // Cardinal directions
            Vector2Int.up,
            Vector2Int.down,
            Vector2Int.left,
            Vector2Int.right,
            
            // Diagonal directions
            new Vector2Int(1, 1),   // Up-Right
            new Vector2Int(1, -1),  // Down-Right
            new Vector2Int(-1, 1),  // Up-Left
            new Vector2Int(-1, -1)  // Down-Left
        };
    }
}
