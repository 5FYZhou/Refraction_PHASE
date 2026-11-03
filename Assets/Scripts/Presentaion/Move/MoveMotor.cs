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
            var scale = transform.localScale;
            scale.x = dir.x > 0 ? 1f : -1f;
            transform.localScale = scale;
        }

        public void MoveTowards(Vector2 dir, float speed)
        {
            FaceTowards(dir);
            _ledger.MoveTowards(dir, speed);
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
