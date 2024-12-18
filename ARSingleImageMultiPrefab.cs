using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;

public class ARSingleImageMultiPrefab : MonoBehaviour
{
    public ARTrackedImageManager trackedImageManager; // Reference to ARTrackedImageManager
    public GameObject[] prefabs; // Array of prefabs for the single image
    public Button forwardButton; // UI Button for next prefab
    public Button backButton; // UI Button for previous prefab

    private List<GameObject> spawnedPrefabs = new List<GameObject>();
    private Dictionary<GameObject, Vector3> initialPositions = new Dictionary<GameObject, Vector3>(); // Store initial positions
    private int currentIndex = 0;

    private ComponentHighlighter currentHighlighter; // Track the currently highlighted component
    private LabelManager currentLabelManager; // Track the currently active label manager

    private float initialPinchDistance;
    private Vector3 initialScale;

    private Dictionary<GameObject, Coroutine> floatingCoroutines = new Dictionary<GameObject, Coroutine>();
    private Animator firstPrefabAnimator;

    public InfoBoxController infoBoxController; // Reference to the InfoBoxController

    private Dictionary<GameObject, Quaternion> rotationOffsets = new Dictionary<GameObject, Quaternion>();
    private Dictionary<GameObject, ARTrackedImage> prefabToTrackedImageMap = new Dictionary<GameObject, ARTrackedImage>();


    


    void Start()
    {
        if (trackedImageManager == null)
        {
            Debug.LogError("ARTrackedImageManager not assigned!");
            return;
        }

        // Subscribe to ARTrackedImageManager events
        trackedImageManager.trackedImagesChanged += OnTrackedImagesChanged;

        // Assign button listeners
        forwardButton.onClick.AddListener(() => TogglePrefab(true));
        backButton.onClick.AddListener(() => TogglePrefab(false));
    }

    void Update()
    {
        HandleTouchInput(); // Check for touch or mouse input for highlighting, scaling, and rotation
    }

    private void OnTrackedImagesChanged(ARTrackedImagesChangedEventArgs eventArgs)
    {
        foreach (var trackedImage in eventArgs.added)
        {
            HandleNewTrackedImage(trackedImage);
        }

        foreach (var trackedImage in eventArgs.updated)
        {
            if (trackedImage.trackingState == UnityEngine.XR.ARSubsystems.TrackingState.Tracking)
            {
                UpdateTrackedImage(trackedImage);
            }
        }

        foreach (var trackedImage in eventArgs.removed)
        {
            RemoveTrackedImage(trackedImage);
        }
    }

    private void HandleNewTrackedImage(ARTrackedImage trackedImage)
    {
        if (trackedImage.referenceImage.name == "MyImage")
        {
            if (spawnedPrefabs.Count == 0)
            {
                for (int i = 0; i < prefabs.Length; i++)
                {
                    GameObject spawnedPrefab = Instantiate(prefabs[i], trackedImage.transform.position, trackedImage.transform.rotation);
                    spawnedPrefab.SetActive(false);
                    spawnedPrefabs.Add(spawnedPrefab);

                    // Store the ARTrackedImage reference
                    prefabToTrackedImageMap[spawnedPrefab] = trackedImage;

                    // Initialize rotation offset
                    rotationOffsets[spawnedPrefab] = Quaternion.identity;

                    // Store the initial position
                    initialPositions[spawnedPrefab] = spawnedPrefab.transform.position;
                }

                // Activate the first prefab
                spawnedPrefabs[0].SetActive(true);
                firstPrefabAnimator = spawnedPrefabs[0].GetComponent<Animator>();
            }
        }
    }


    private void UpdateTrackedImage(ARTrackedImage trackedImage)
    {
        if (trackedImage.referenceImage.name == "MyImage")
        {
            GameObject activePrefab = spawnedPrefabs[currentIndex];

            // Update position
            activePrefab.transform.position = new Vector3(
                trackedImage.transform.position.x, 
                trackedImage.transform.position.y + 0.05f, 
                trackedImage.transform.position.z
            );

            // Combine tracked rotation with the manual rotation offset
            if (rotationOffsets.ContainsKey(activePrefab))
            {
                activePrefab.transform.rotation = trackedImage.transform.rotation * rotationOffsets[activePrefab];
            }
            else
            {
                activePrefab.transform.rotation = trackedImage.transform.rotation;
            }
        }
    }


    private void RemoveTrackedImage(ARTrackedImage trackedImage)
    {
        if (trackedImage.referenceImage.name == "MyImage")
        {
            // Deactivate all prefabs when the image is removed
            foreach (var prefab in spawnedPrefabs)
            {
                prefab.SetActive(false);
                StopFloating(prefab);
            }
        }
    }

    private void TogglePrefab(bool forward)
    {
        if (spawnedPrefabs.Count == 0) return;

        // Deactivate the current prefab
        GameObject currentPrefab = spawnedPrefabs[currentIndex];
        currentPrefab.SetActive(false);
        StopFloating(currentPrefab);

        // Stop any audio playing for the current prefab
        AudioSource currentAudioSource = currentPrefab.GetComponent<AudioSource>();
        if (currentAudioSource != null && currentAudioSource.isPlaying)
        {
            currentAudioSource.Stop();
            Debug.Log($"Stopped audio for prefab: {currentPrefab.name}");
        }

        // Reset the position of the current prefab
        if (initialPositions.ContainsKey(currentPrefab))
        {
            currentPrefab.transform.position = initialPositions[currentPrefab];
        }

        // Calculate the new index
        currentIndex = forward ? (currentIndex + 1) % spawnedPrefabs.Count : (currentIndex - 1 + spawnedPrefabs.Count) % spawnedPrefabs.Count;

        // Activate the new prefab
        GameObject newPrefab = spawnedPrefabs[currentIndex];

        // Handle the first prefab differently if needed
        if (currentIndex == 0)
        {
            stopFirstPrefab(newPrefab);

            // Trigger animation for the first prefab
            if (firstPrefabAnimator != null)
            {
                firstPrefabAnimator.SetTrigger("PlayAnimation"); // Ensure your animation has a trigger called "PlayAnimation"
            }
        }
        else
        {
            newPrefab.SetActive(true);

            // Reset position for the new prefab
            if (initialPositions.ContainsKey(newPrefab))
            {
                newPrefab.transform.position = initialPositions[newPrefab];
            }

            StartFloating(newPrefab);
        }

        // Notify InfoBoxController about the prefab change
        if (infoBoxController != null)
        {
            Debug.Log($"Updating InfoBoxController with index: {currentIndex}");
            infoBoxController.SetCurrentPrefabIndex(currentIndex);
        }
        else
        {
            Debug.LogWarning("InfoBoxController is not assigned!");
        }

        // Play audio for the new prefab if it has an AudioSource
        AudioSource newAudioSource = newPrefab.GetComponent<AudioSource>();
        if (newAudioSource != null && newAudioSource.clip != null)
        {
            newAudioSource.Play();
            Debug.Log($"Playing audio for prefab: {newPrefab.name}");
        }
        else
        {
            Debug.LogWarning($"No AudioSource or AudioClip found for prefab: {newPrefab.name}");
        }
    }



    private void stopFirstPrefab(GameObject firstPrefab)
    {
        firstPrefab.SetActive(true);
        if (initialPositions.ContainsKey(firstPrefab))
        {
            firstPrefab.transform.position = initialPositions[firstPrefab];
        }
    }

private void HandleTouchInput()
{
    if (spawnedPrefabs.Count == 0) return;

    GameObject activePrefab = spawnedPrefabs[currentIndex];

    if (Input.touchCount == 1)
    {
        Touch touch = Input.GetTouch(0);

        if (touch.phase == TouchPhase.Began)
        {
            Ray ray = Camera.main.ScreenPointToRay(touch.position);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit))
            {
                // Highlight Logic
                ComponentHighlighter highlighter = hit.collider.GetComponent<ComponentHighlighter>();
                if (highlighter != null)
                {
                    if (currentHighlighter != null && currentHighlighter != highlighter)
                    {
                        currentHighlighter.RemoveHighlight(); // Remove previous highlight
                    }
                    currentHighlighter = highlighter;
                    currentHighlighter.Highlight(); // Highlight the selected component
                }

                // Label Logic
                LabelManager labelManager = hit.collider.GetComponent<LabelManager>();
                if (labelManager != null)
                {
                    // Ensure only the current label is shown, and previous labels are hidden
                    if (currentLabelManager != null && currentLabelManager != labelManager)
                    {
                        currentLabelManager.HideLabel(); // Hide the previously shown label
                    }
                    currentLabelManager = labelManager;
                    currentLabelManager.ShowLabel(); // Show the label for the selected component
                }
            }
        }

        if (touch.phase == TouchPhase.Moved)
        {
            Vector2 delta = touch.deltaPosition;

            Quaternion targetRotation = activePrefab.transform.rotation;

            // Handle rotation for the first prefab (index 0)
            if (currentIndex == 0)
            {
                // Y-axis rotation only (yaw)
                Quaternion horizontalRotationChange = Quaternion.Euler(0, -delta.x * 0.1f, 0);
                if (rotationOffsets.ContainsKey(activePrefab))
                {
                    rotationOffsets[activePrefab] *= horizontalRotationChange;
                }
                else
                {
                    rotationOffsets[activePrefab] = horizontalRotationChange;
                }

                // Combine tracked rotation with the manual rotation offset
                if (prefabToTrackedImageMap.TryGetValue(activePrefab, out ARTrackedImage associatedTrackedImage))
                {
                    targetRotation = associatedTrackedImage.transform.rotation * rotationOffsets[activePrefab];
                }
            }
            else
            {
                // For prefabs 2, 3, and 4: Y-axis (yaw) and Z-axis (roll)
                if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y)) // Prioritize horizontal movement
                {
                    Quaternion horizontalRotationChange = Quaternion.Euler(0, -delta.x * 0.1f, 0);
                    if (rotationOffsets.ContainsKey(activePrefab))
                    {
                        rotationOffsets[activePrefab] *= horizontalRotationChange;
                    }
                    else
                    {
                        rotationOffsets[activePrefab] = horizontalRotationChange;
                    }
                }
                else // Prioritize vertical movement
                {
                    Quaternion verticalRotationChange = Quaternion.Euler(0, 0, -delta.y * 0.1f);
                    if (rotationOffsets.ContainsKey(activePrefab))
                    {
                        rotationOffsets[activePrefab] *= verticalRotationChange;
                    }
                    else
                    {
                        rotationOffsets[activePrefab] = verticalRotationChange;
                    }
                }

                // Combine tracked rotation with the manual rotation offset
                if (prefabToTrackedImageMap.TryGetValue(activePrefab, out ARTrackedImage associatedTrackedImage))
                {
                    targetRotation = associatedTrackedImage.transform.rotation * rotationOffsets[activePrefab];
                }
            }

            // Smoothly interpolate towards the target rotation
            activePrefab.transform.rotation = Quaternion.Lerp(activePrefab.transform.rotation, targetRotation, Time.deltaTime * 10f);
        }
    }

    // Pinch-to-Zoom
    else if (Input.touchCount == 2)
    {
        Touch touch1 = Input.GetTouch(0);
        Touch touch2 = Input.GetTouch(1);

        if (touch1.phase == TouchPhase.Moved || touch2.phase == TouchPhase.Moved)
        {
            float currentPinchDistance = Vector2.Distance(touch1.position, touch2.position);

            if (initialPinchDistance == 0)
            {
                initialPinchDistance = currentPinchDistance;
                initialScale = activePrefab.transform.localScale;
            }

            float scaleFactor = currentPinchDistance / initialPinchDistance;
            activePrefab.transform.localScale = initialScale * scaleFactor;
        }
    }
    else
    {
        initialPinchDistance = 0; // Reset pinch distance when not pinching
    }
}

    private void StartFloating(GameObject prefab)
    {

        if (floatingCoroutines.ContainsKey(prefab) && floatingCoroutines[prefab] != null) return;

        Coroutine floatingCoroutine = StartCoroutine(FloatingEffect(prefab));
        floatingCoroutines[prefab] = floatingCoroutine;
    }

    private void StopFloating(GameObject prefab)
    {
        if (floatingCoroutines.ContainsKey(prefab) && floatingCoroutines[prefab] != null)
        {
            StopCoroutine(floatingCoroutines[prefab]);
            floatingCoroutines.Remove(prefab);
        }
    }

    private IEnumerator FloatingEffect(GameObject prefab)
    {
        float floatSpeed = 1f; // Speed of the floating motion
        float floatAmplitude = 0.03f; // Smaller amplitude for subtle floating
        float baseHeightOffset = 0.1f; // Base height above the image

        Vector3 startPosition = initialPositions.ContainsKey(prefab) ? initialPositions[prefab] : prefab.transform.position;

        while (true)
        {
            float newY = startPosition.y + baseHeightOffset + Mathf.Sin(Time.time * floatSpeed) * floatAmplitude;
            prefab.transform.position = new Vector3(startPosition.x, newY, startPosition.z);
            yield return null;
        }
    }

    private void UpdateInfoBoxController()
    {
        if (infoBoxController != null)
        {
            infoBoxController.SetCurrentPrefabIndex(currentIndex);
        }
    }
}