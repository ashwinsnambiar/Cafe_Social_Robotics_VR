namespace ExpObj
{
    using System.Collections.Generic;
    using UnityEngine;
    using UnityEngine.InputSystem;

    public class ExplosionController : MonoBehaviour
    {
        public List<ExplosiveObject> explosiveObjects = new List<ExplosiveObject>();

        public ExplosiveObject explosiveObject;

        public List<Transform> explosiveObjTransforms = new List<Transform>();

        private void Update()
        {
            // Diagnose input: make sure Update runs and keyboard is available
            if (Keyboard.current == null)
            {
                Debug.LogWarning("ExplosionController: Keyboard.current is null. Ensure the Input System package is installed and 'Active Input Handling' is set to 'Input System Package (New)'.");
                return;
            }

            if (!Application.isFocused)
            {
                // Editor or game window not focused — key presses won't be registered
                Debug.Log("ExplosionController: Application not focused. Click the Game view and press keys while in Play mode.");
            }

            // Using the new Input System: press E to explode, R to respawn
            if (Keyboard.current.eKey.wasPressedThisFrame)
            {
                Debug.Log("ExplosionController: E pressed — exploding all objects.");
                ExplodeAll();
            }

            if (Keyboard.current.rKey.wasPressedThisFrame)
            {
                Debug.Log("ExplosionController: R pressed — respawning objects.");
                RespawnObjects();
            }
        }

        void ExplodeAll()
        {
            for (int i = 0; i < explosiveObjects.Count; i++)
            {
                if (explosiveObjects[i] != null)
                {
                    explosiveObjects[i].Explode(); // calling function to make objects explode
                }
            }
            explosiveObjects.Clear();
        }

        void RespawnObjects()
        {
            foreach (Transform t in explosiveObjTransforms)
            {
                GameObject newObj = Instantiate(explosiveObject.gameObject, t.position, Quaternion.identity);
                explosiveObjects.Add(newObj.GetComponent<ExplosiveObject>());
            }
        }
    }
}