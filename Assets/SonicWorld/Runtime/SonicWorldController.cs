using UnityEngine;

namespace SonicWorld
{
    public sealed class SonicWorldController : MonoBehaviour
    {
        [SerializeField] private SonicMusicPlayer musicPlayer;
        [SerializeField] private Rigidbody[] resetBodies;

        private Vector3[] startPositions;
        private Quaternion[] startRotations;

        private void Awake()
        {
            if (resetBodies == null)
                resetBodies = new Rigidbody[0];

            startPositions = new Vector3[resetBodies.Length];
            startRotations = new Quaternion[resetBodies.Length];
            for (int i = 0; i < resetBodies.Length; i++)
            {
                if (resetBodies[i] == null)
                    continue;
                startPositions[i] = resetBodies[i].position;
                startRotations[i] = resetBodies[i].rotation;
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Space))
                musicPlayer?.TogglePlayback();
            if (Input.GetKeyDown(KeyCode.N))
                musicPlayer?.Next();
            if (Input.GetKeyDown(KeyCode.R))
                ResetObjects();

            for (int i = 0; i < resetBodies.Length; i++)
            {
                if (resetBodies[i] != null && resetBodies[i].position.y < -5f)
                    ResetObject(i);
            }
        }

        public void ResetObjects()
        {
            for (int i = 0; i < resetBodies.Length; i++)
                ResetObject(i);
        }

        private void ResetObject(int index)
        {
            Rigidbody body = resetBodies[index];
            if (body == null)
                return;

            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.position = startPositions[index];
            body.rotation = startRotations[index];
            body.Sleep();
        }
    }
}
