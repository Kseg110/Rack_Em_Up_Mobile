using UnityEngine;

/// <summary>
/// Stateless collision resolution helper for a kinematic BilliardBall.
/// Resolves wall reflections, ball-to-ball elastic collisions, and ball-to-enemy
/// momentum transfers. Instantiated by BilliardBall and called per collision event.
/// </summary>
public class BallCollisionResolver
{
    private readonly float restitution;
    private readonly float ballRadius;

    public BallCollisionResolver(float restitution, float ballRadius)
    {
        this.restitution = restitution;
        this.ballRadius  = ballRadius;
    }

    public Vector3 ResolveWall(Rigidbody rb, Vector3 velocity, Vector2 spin,
                                Vector3 normal, Vector3 hitPoint,
                                out Vector2 spinOut, out Vector3 angularVelocityScale)
    {
        Vector3 newVelocity = Vector3.Reflect(velocity, normal) * restitution;

        spinOut              = spin * 0.9f;
        angularVelocityScale = new Vector3(0.9f, 0.9f, 0.9f);

        rb.MovePosition(hitPoint + normal * (ballRadius + 0.001f));

        Debug.DrawRay(hitPoint, normal * 0.5f, Color.red, 0.2f);
        return newVelocity;
    }

    /// <summary>Returns updated velocities for both balls via out params.</summary>
    public void ResolveBallToBall(Rigidbody rb, BilliardBall other,
                                   Vector3 myVelocity, Vector3 otherVelocity, Vector3 normal,
                                   out Vector3 myNewVelocity, out Vector3 otherNewVelocity)
    {
        float mySpeed    = Vector3.Dot(myVelocity, normal);
        float otherSpeed = Vector3.Dot(otherVelocity, -normal);

        if (mySpeed < 0f)
        {
            myNewVelocity    = myVelocity;
            otherNewVelocity = otherVelocity;
            return;
        }

        Vector3 myNormalVel    = normal * mySpeed;
        Vector3 otherNormalVel = -normal * otherSpeed;

        myNewVelocity    = (myVelocity    - myNormalVel    + otherNormalVel) * restitution;
        otherNewVelocity = (otherVelocity - otherNormalVel + myNormalVel)    * restitution;

        rb.MovePosition(rb.position - normal * (ballRadius * 0.1f));
    }

    public Vector3 ResolveBallToEnemy(Rigidbody rb, EnemyBallBase enemyBall,
                                       Vector3 velocity, Vector3 normal)
    {
        Vector3 impulse = velocity * rb.mass * restitution;
        enemyBall.ApplyForce(impulse);
        return Vector3.Reflect(velocity, normal) * restitution * 0.5f;
    }
}