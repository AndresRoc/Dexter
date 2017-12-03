﻿using UnityEngine;

public static class Constraints {
  public static void ConstrainToPoint(this Transform transform, Vector3 oldPoint, Vector3 newPoint, bool fastApproximate = true) {
    Quaternion rotation = Quaternion.FromToRotation(transform.position - oldPoint, transform.position - newPoint);
    transform.position = fastApproximate ? transform.position.FastConstrainDistance(newPoint, (transform.position - oldPoint).sqrMagnitude) :
                                           transform.position.ConstrainDistance(newPoint, (transform.position - oldPoint).magnitude);
    transform.rotation = rotation * transform.rotation;
  }

  public static void ConstrainToPoint(this Transform transform, Vector3 oldPoint, Vector3 newPoint, Vector3 anchor) {
    Quaternion rotation = Quaternion.FromToRotation(oldPoint - anchor, newPoint - anchor);
    transform.RotateAroundPivot(anchor, rotation);
    transform.position += newPoint - oldPoint.RotateAroundPivot(anchor, rotation);
  }

  public static void RotateToPoint(this Transform transform, Vector3 oldPoint, Vector3 newPoint, float alpha = 1f) {
    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.FromToRotation(transform.position - oldPoint, transform.position - newPoint) * transform.rotation, alpha);
  }

  public static Vector3 ConstrainToCone(this Vector3 point, Vector3 origin, Vector3 normalDirection, float minDot) {
    return (point - origin).ConstrainToNormal(normalDirection, minDot) + origin;
  }

  public static Vector3 ConstrainToNormal(this Vector3 direction, Vector3 normalDirection, float maxAngle) {
    if (maxAngle <= 0f) return normalDirection.normalized * direction.magnitude; if (maxAngle >= 180f) return direction;
    float angle = Mathf.Acos(Mathf.Clamp(Vector3.Dot(direction.normalized, normalDirection.normalized), -1f, 1f)) * Mathf.Rad2Deg;
    return Vector3.Slerp(direction.normalized, normalDirection.normalized, (angle - maxAngle) / angle) * direction.magnitude;
  }

  public static Vector3 ConstrainToSegment(this Vector3 position, Vector3 a, Vector3 b) {
    Vector3 ba = b - a;
    return Vector3.Lerp(a, b, Vector3.Dot(position - a, ba) / ba.sqrMagnitude);
  }

  public static Vector3 ConstrainToCapsule(this Vector3 position, Vector3 a, Vector3 b, float radius, bool toSurface = false) {
    Vector3 onSegment = ConstrainToSegment(position, a, b);
    Vector3 displacement = position - onSegment;
    float magnitude = displacement.magnitude;
    return magnitude > radius ? onSegment + (displacement.normalized * radius) : position;
  }

  public static Vector3 ClosestPointOnCapsule(Vector3 point, CapsuleCollider collider) {
    Vector3 offset = (collider.direction == 0 ? Vector3.right : Vector3.up) * Mathf.Clamp01((collider.height * 0.5f) - collider.radius);
    Vector3 onSegment = ConstrainToSegment(point, collider.transform.TransformPoint(collider.center + offset), collider.transform.TransformPoint(collider.center - offset));
    return onSegment + ((point - onSegment).normalized * collider.radius);
  }

  //Ack late night function; horrible horrible code
  public static Vector4 ClosestPointCapsuleOnPlane(CapsuleCollider collider, Vector3 point, Vector3 normal) {
    Vector3 offset = (collider.direction == 0 ? Vector3.right : Vector3.up) * Mathf.Clamp01((collider.height * 0.5f) - collider.radius);

    Vector3 capsuleEnd = collider.transform.TransformPoint(collider.center + offset);
    Vector4 planeCandidate1 = Vector3.ProjectOnPlane(capsuleEnd - point, normal) + point;
    planeCandidate1 = new Vector4(planeCandidate1.x, planeCandidate1.y, planeCandidate1.z, (-Mathf.Sign(Vector3.Dot(capsuleEnd - point, normal)) * Vector3.Distance(planeCandidate1, capsuleEnd)) - collider.radius);

    capsuleEnd = collider.transform.TransformPoint(collider.center - offset);
    Vector4 planeCandidate2 = Vector3.ProjectOnPlane(capsuleEnd - point, normal) + point;
    planeCandidate2 = new Vector4(planeCandidate2.x, planeCandidate2.y, planeCandidate2.z, (-Mathf.Sign(Vector3.Dot(capsuleEnd - point, normal)) * Vector3.Distance(planeCandidate2, capsuleEnd)) - collider.radius);

    if (Vector3.Dot(normal, collider.transform.rotation * offset) > 0f) {
      return planeCandidate2;
    } else {
      return planeCandidate1;
    }
  }

  public static Vector3 ConstrainDistance(this Vector3 position, Vector3 anchor, float distance) {
    return anchor + ((position - anchor).normalized * distance);
  }

  public static Vector3 FastConstrainDistance(this Vector3 position, Vector3 anchor, float sqrDistance) {
    Vector3 offset = (position - anchor);
    offset *= (sqrDistance / (Vector3.Dot(offset, offset) + sqrDistance) - 0.5f) * 2f;
    return position + offset;
  }

  public static Quaternion ConstrainRotationToCone(Quaternion rotation, Vector3 constraintAxis, Vector3 objectLocalAxis, float maxAngle) {
    return Quaternion.FromToRotation(rotation * objectLocalAxis, ConstrainToNormal(rotation * objectLocalAxis, constraintAxis, maxAngle)) * rotation;
  }

  public static Quaternion ConstrainRotationToConeWithTwist(Quaternion rotation, Vector3 constraintAxis, Vector3 objectLocalAxis, float maxAngle, float maxTwistAngle) {
    Quaternion coneRotation = ConstrainRotationToCone(rotation, constraintAxis, objectLocalAxis, maxAngle);
    Vector3 perpendicularAxis = Vector3.Cross(constraintAxis, Quaternion.Euler(10f, 0f, 0f) * constraintAxis).normalized;
    Quaternion coneConstraint = Quaternion.FromToRotation(objectLocalAxis, coneRotation * objectLocalAxis);
    return ConstrainRotationToCone(coneRotation, coneConstraint * perpendicularAxis, perpendicularAxis, maxTwistAngle);
  }

  private static int sign(float num) {
    return num == 0 ? 0 : (num >= 0 ? 1 : -1);
  }

  public static Vector3 ConstrainToTriangle(this Vector3 position, Vector3 a, Vector3 b, Vector3 c) {
    Vector3 normal = Vector3.Cross(b - a, a - c);
    bool outsidePlaneBounds =
    (sign(Vector3.Dot(Vector3.Cross(b - a, normal), position - a)) +
     sign(Vector3.Dot(Vector3.Cross(c - b, normal), position - b)) +
     sign(Vector3.Dot(Vector3.Cross(a - c, normal), position - c)) < 2);
    if (!outsidePlaneBounds) {
      //Project onto plane
      return Vector3.ProjectOnPlane(position, normal);
    } else {
      //Constrain to edges
      Vector3 edge1 = position.ConstrainToSegment(a, b);
      Vector3 edge2 = position.ConstrainToSegment(b, c);
      Vector3 edge3 = position.ConstrainToSegment(c, a);
      float sm1 = Vector3.SqrMagnitude(position - edge1);
      float sm2 = Vector3.SqrMagnitude(position - edge2);
      float sm3 = Vector3.SqrMagnitude(position - edge3);
      if (sm1 < sm2) {
        if (sm1 < sm3) {
          return edge1;
        }
      } else {
        if (sm2 < sm3) {
          return edge2;
        }
      }
      return edge3;
    }
  }

  public static void RotateAroundPivot(this Transform transform, Vector3 pivot, Quaternion rotation, float alpha = 1f) {
    Quaternion toRotate = Quaternion.Slerp(Quaternion.identity, rotation, alpha);
    transform.position = transform.position.RotateAroundPivot(pivot, rotation);
    transform.rotation = toRotate * transform.rotation;
  }

  public static Vector3 RotateAroundPivot(this Vector3 point, Vector3 pivot, Quaternion rotation) {
    return (rotation * (point - pivot)) + pivot;
  }

  //Credit to Sam Hocevar of LolEngine
  //lolengine.net/blog/2013/09/21/picking-orthogonal-vector-combing-coconuts
  public static Vector3 perpendicular(this Vector3 vec) {
    return Mathf.Abs(vec.x) > Mathf.Abs(vec.z) ? new Vector3(-vec.y, vec.x, 0f)
                                               : new Vector3(0f, -vec.z, vec.y);
  }
}