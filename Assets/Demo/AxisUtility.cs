using UnityEngine;

namespace LR.Demos
{

	public sealed class AxisUtility : MonoBehaviour
	{

		[SerializeField]
		private float m_translationCoef = 1.0f;

		[SerializeField]
		private float m_zoomCoef = 1.0f;

		[SerializeField]
		private float m_rotationCoef = 1.0f;

		public float translationCoef
		{
			set
			{
				m_translationCoef = value;
			}
			get
			{
				return m_translationCoef;
			}
		}

		public float zoomCoef
		{
			set
			{
				m_zoomCoef = value;
			}
			get
			{
				return m_zoomCoef;
			}
		}

		public float rotationCoef
		{
			set
			{
				m_rotationCoef = value;
			}
			get
			{
				return m_rotationCoef;
			}
		}

		public Matrix4x4 GetLookAt()
		{
			return Matrix4x4.Rotate(transform.rotation).inverse * Matrix4x4.Translate(transform.position);
		}

		private void Update()
		{
			var mouseDelta = new Vector3(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"), Input.GetAxis("Mouse ScrollWheel"));
			if (Input.GetMouseButton(1))
			{
				transform.rotation = Quaternion.AngleAxis(-mouseDelta.x * m_rotationCoef, Vector3.up) * Quaternion.AngleAxis(mouseDelta.y * m_rotationCoef, transform.right) * transform.rotation;
			}
			if (Input.GetMouseButton(2))
			{
				var delta = transform.right * mouseDelta.x * m_translationCoef + transform.up * mouseDelta.y * m_translationCoef;
				transform.position += delta;
			}
			if (mouseDelta.z != 0.0f)
			{
				var delta = transform.forward * mouseDelta.z * m_zoomCoef;
				transform.position += delta;
			}
		}

	}

}
