using Refraction.Data;
using Refraction.Logic.Move;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Refraction.Presentation
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class MoveMotor : MonoBehaviour, IMoveMotor
    {
        [SerializeField]
        private PlayerConfig config;
        private Rigidbody2D _rb;
        private MoveLedger _ledger;

        public Rigidbody2D Rb { get => _rb; }

        public float Speed => config.speed;

        private Vector2 _face;
        public Vector2 Face
        {
            get => _face;
            set
            {
                if (value.sqrMagnitude <= 0f) return;

                _face = value;

                if (value.x == 0f) return;

                Vector3 scale = transform.localScale;
                scale.x = Mathf.Abs(scale.x) * (value.x < 0f ? -1f : 1f);
                transform.localScale = scale;
            }
        }

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _ledger = new MoveLedger(
                readVelocity: () => _rb.velocity,
                writeVelocity: v => _rb.velocity = v);
        }

        public void BeginStep(float now, float deltaTime)
        {
            _ledger.BeginStep(now, deltaTime);
        }

        public void FaceTowards(Vector2 dir)
        {
            if (dir.sqrMagnitude <= 0f) return;
            Face = dir;
        }

        public void MoveTowards(Vector2 dir, float speed)
        {
            FaceTowards(dir);
            _ledger.MoveTowards(dir, speed);
        }

        public void AddImpulse(Vector2 deltaVelocity)
        {
            _ledger.AddImpulse(deltaVelocity);
        }

        public void StopMove()
        {
            _ledger.StopMove();
        }

        public void Commit()
        {
            _ledger.Commit();
        }
    }
}
