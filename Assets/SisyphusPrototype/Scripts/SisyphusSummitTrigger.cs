using UnityEngine;

namespace SisyphusPrototype
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class SisyphusSummitTrigger : MonoBehaviour
    {
        [SerializeField] SisyphusRoundDirector director;

        public void Configure(SisyphusRoundDirector roundDirector)
        {
            director = roundDirector;
        }

        void Reset()
        {
            GetComponent<BoxCollider>().isTrigger = true;
        }

        void OnTriggerEnter(Collider other)
        {
            Rigidbody body = other.attachedRigidbody;
            if (director != null && body != null && director.IsTargetBoulder(body))
                director.ReachSummit();
        }
    }
}
