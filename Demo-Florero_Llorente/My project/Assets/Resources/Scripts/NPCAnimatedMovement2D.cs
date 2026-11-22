using UnityEngine;

/// <summary>
/// Reusable 2D NPC movement + animation driver.
/// - Wander mode: picks random points inside a radius (around the spawn point or the current position).
/// - Patrol mode: walks through a list of waypoints (looping or ping-pong).
/// - Constantly alternates between Moving and Idle (pause) states; it never walks forever.
/// - After a configurable amount of active time it returns to its spawn point, pauses there, then resumes.
/// - Drives the Animator with a 4-direction (Up/Down/Left/Right) facing, ready for Blend Trees.
/// - Uses Rigidbody2D velocity so Tilemap colliders block it properly.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(AudioSource))]




public class NPCAnimatedMovement2D : MonoBehaviour
{
    
    
[Header("Audio / Pasos")]
[SerializeField] private AudioSource audioSource;
[SerializeField] private AudioClip[] footstepClips;
[SerializeField, Min(0.1f)] private float stepInterval = 0.4f; // Tiempo entre cada paso
private float stepTimer;


    // ───────────────────────── Types ─────────────────────────

    public enum MovementMode { Wander, Patrol }

    /// <summary>What the MoveX/MoveY parameters contain while the NPC is paused.</summary>
    public enum IdleAnimationMode
    {
        /// <summary>MoveX/MoveY keep the last facing direction (use IsWalking to switch Idle/Walk).</summary>
        KeepFacingInMoveParams,
        /// <summary>MoveX/MoveY are set to (0,0); the facing is sent through FacingX/FacingY instead.</summary>
        ZeroMoveParams
    }

    public enum InitialFacing { Down, Up, Left, Right }

    private enum State { Idle, Moving, ReturningHome, HomePause }

    // ───────────────────── Inspector settings ─────────────────────

    [Header("Movement mode")]
    [SerializeField] private MovementMode mode = MovementMode.Wander;
    [SerializeField, Min(0f)] private float moveSpeed = 2f;
    [Tooltip("Distance to the target at which it is considered reached.")]
    [SerializeField, Min(0.01f)] private float arrivalThreshold = 0.1f;

    [Header("Wander (random)")]
    [Tooltip("Maximum radius for random destinations.")]
    [SerializeField, Min(0.1f)] private float wanderRadius = 4f;
    [Tooltip("True: radius measured from the spawn point. False: from the current position.")]
    [SerializeField] private bool wanderFromOrigin = true;
    [Tooltip("Minimum distance of a new destination from the current position (avoids tiny moves).")]
    [SerializeField, Min(0f)] private float minWanderDistance = 1f;
    [Tooltip("Attempts to find a valid (obstacle-free) point before giving up and pausing again.")]
    [SerializeField, Min(1)] private int maxPointAttempts = 10;

    [Header("Patrol (route)")]
    [Tooltip("Waypoints (empty Transforms in the scene; do NOT parent them to the NPC).")]
    [SerializeField] private Transform[] patrolPoints;
    [Tooltip("True: A-B-C-B-A... | False: A-B-C-A-B-C...")]
    [SerializeField] private bool pingPong = false;

    [Header("Pauses (Idle)")]
    [SerializeField, Min(0f)] private float minPauseTime = 1f;
    [SerializeField, Min(0f)] private float maxPauseTime = 3f;

    [Header("Return to origin")]
    [SerializeField] private bool returnToOrigin = true;
    [Tooltip("Seconds of activity (moving + pausing) before returning to the spawn point.")]
    [SerializeField, Min(1f)] private float returnInterval = 30f;
    [Tooltip("Seconds spent waiting at the spawn point before resuming the routine.")]
    [SerializeField, Min(0f)] private float homePauseTime = 4f;

    [Header("Collisions and obstacles")]
    [Tooltip("Obstacle layers (wall Tilemap, props...). Do NOT include the NPC's own layer.")]
    [SerializeField] private LayerMask obstacleMask;
    [Tooltip("Approximate NPC radius, used to validate destinations and paths.")]
    [SerializeField, Min(0.05f)] private float agentRadius = 0.3f;
    [Tooltip("Seconds without progress against an obstacle before the target is abandoned.")]
    [SerializeField, Min(0.2f)] private float stuckTimeout = 1f;

    [Header("Rigidbody2D auto-setup")]
    [Tooltip("Sets gravity 0, frozen rotation and interpolation (recommended for top-down games).")]
    [SerializeField] private bool autoConfigureRigidbody = true;

    [Header("Animator")]
    [Tooltip("Optional. If empty, the Animator on this GameObject is used.")]
    [SerializeField] private Animator animator;
    [SerializeField] private InitialFacing initialFacing = InitialFacing.Down;
    [SerializeField] private IdleAnimationMode idleAnimationMode = IdleAnimationMode.KeepFacingInMoveParams;
    [Tooltip("Higher values make the NPC less likely to flip its facing when moving diagonally.")]
    [SerializeField, Min(1f)] private float directionHysteresis = 1.2f;

    [Header("Animator parameter names")]
    [SerializeField] private string moveXParam = "MoveX";          // Float
    [SerializeField] private string moveYParam = "MoveY";          // Float
    [SerializeField] private string isWalkingParam = "IsWalking";  // Bool
    [Tooltip("Optional Float. Always holds the last facing direction (useful with ZeroMoveParams).")]
    [SerializeField] private string facingXParam = "FacingX";
    [Tooltip("Optional Float. Always holds the last facing direction (useful with ZeroMoveParams).")]
    [SerializeField] private string facingYParam = "FacingY";

    // ───────────────────── Internal state ─────────────────────

    private Rigidbody2D rb;
    private State state = State.Idle;

    private Vector2 homePosition;     // Spawn point
    private Vector2 currentTarget;    // Current destination
    private float stateTimer;         // Countdown for pauses
    private float activeTimer;        // Active time since the last return home

    private int patrolIndex;
    private int patrolDirection = 1;

    // Stuck detection
    private const float StuckCheckInterval = 0.25f;
    private float stuckCheckTimer;
    private float stuckAccumulated;
    private Vector2 lastCheckPosition;

    // Facing (always a cardinal unit vector: up, down, left or right)
    private Vector2 facing = Vector2.down;

    // Animator cache (hashes + which parameters actually exist, to avoid warnings and wasted calls)
    private bool animatorReady;
    private int hashMoveX, hashMoveY, hashIsWalking, hashFacingX, hashFacingY;
    private bool hasMoveX, hasMoveY, hasIsWalking, hasFacingX, hasFacingY;

    // Last values sent to the Animator (only write when something changes)
    private float sentMoveX = float.NaN, sentMoveY = float.NaN;
    private float sentFacingX = float.NaN, sentFacingY = float.NaN;
    private int sentWalking = -1; // -1 = never sent

    // ───────────────────── Public API ─────────────────────

    /// <summary>True while the NPC is travelling to a destination.</summary>
    public bool IsMoving => state == State.Moving || state == State.ReturningHome;

    /// <summary>Last facing direction as a cardinal unit vector.</summary>
    public Vector2 Facing => facing;

    /// <summary>Forces the NPC to go back to its spawn point right away.</summary>
    public void ForceReturnHome() => StartReturningHome();

    // Velocity wrapper: Unity 6 renamed velocity to linearVelocity.
    private Vector2 Velocity
    {
        set
        {
#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = value;
#else
            rb.velocity = value;
#endif
        }
    }

    // ───────────────────── Lifecycle ─────────────────────

    private void Reset()
    {
        // Comfortable defaults when the component is first added.
        var body = GetComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        body.freezeRotation = true;
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (autoConfigureRigidbody)
        {
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }

        homePosition = rb.position;
        facing = GetInitialFacingVector();
        InitializeAnimator();
    }

    private void Start()
    {
        if (mode == MovementMode.Patrol && (patrolPoints == null || patrolPoints.Length == 0))
        {
            Debug.LogWarning($"[{name}] Patrol mode without waypoints. Falling back to Wander.", this);
            mode = MovementMode.Wander;
        }

        // Random initial pause so several NPCs do not move in sync.
        EnterIdle(Random.Range(0f, Mathf.Max(minPauseTime, maxPauseTime)));
    }

    // Timers and decisions run in Update...
    private void Update()
    {
        // Activity time only counts while the NPC follows its normal routine.
        if (state == State.Idle || state == State.Moving)
            activeTimer += Time.deltaTime;

        if (state == State.Idle || state == State.HomePause)
        {
            stateTimer -= Time.deltaTime;
            if (stateTimer <= 0f)
                DecideNextAction();
        }
    }

    // ...physics movement runs in FixedUpdate...
 /*private void FixedUpdate()
    {
        if (!IsMoving)
        {
            Velocity = Vector2.zero;
            stepTimer = 0f; // Reiniciar pasos al pausar
            return;
        }

        Vector2 position = rb.position;
        Vector2 toTarget = currentTarget - position;

        float threshold = Mathf.Max(arrivalThreshold, moveSpeed * Time.fixedDeltaTime);

        // Si ya llegó al destino
        if (toTarget.sqrMagnitude <= threshold * threshold)
        {
            OnArrived();
            return;
        }

        // Movimiento en L estricto (Primero completa el eje X, luego el eje Y)
        // Esto evita por completo las diagonales y el parpadeo al regresar a casa.
        Vector2 direction = Vector2.zero;
        if (Mathf.Abs(toTarget.x) > threshold)
        {
            direction = new Vector2(Mathf.Sign(toTarget.x), 0f);
        }
        else if (Mathf.Abs(toTarget.y) > threshold)
        {
            direction = new Vector2(0f, Mathf.Sign(toTarget.y));
        }

        Velocity = direction * moveSpeed;

        // Actualizar la dirección de la mirada de forma limpia
        if (direction.sqrMagnitude > 0.01f)
        {
            if (Mathf.Abs(direction.x) > 0.1f)
                facing = new Vector2(Mathf.Sign(direction.x), 0f);
            else if (Mathf.Abs(direction.y) > 0.1f)
                facing = new Vector2(0f, Mathf.Sign(direction.y));
        }

        // Control del sonido de los pasos mientras camina
        stepTimer += Time.fixedDeltaTime;
        if (stepTimer >= stepInterval)
        {
            PlayFootstepSound();
            stepTimer = 0f;
        }

        CheckIfStuck(position);
    }
*/

/*private void FixedUpdate()
{
    if (!IsMoving)
    {
        Velocity = Vector2.zero;
        stepTimer = 0f;
        return;
    }

    Vector2 position = rb.position;
    Vector2 toTarget = currentTarget - position;

    float threshold = Mathf.Max(arrivalThreshold, moveSpeed * Time.fixedDeltaTime);

    // Si ya llegó al destino
    if (toTarget.sqrMagnitude <= threshold * threshold)
    {
        OnArrived();
        return;
    }

    // Movimiento en L estricto
    Vector2 direction = Vector2.zero;
    if (Mathf.Abs(toTarget.x) > threshold)
    {
        direction = new Vector2(Mathf.Sign(toTarget.x), 0f);
    }
    else if (Mathf.Abs(toTarget.y) > threshold)
    {
        direction = new Vector2(0f, Mathf.Sign(toTarget.y));
    }

    Velocity = direction * moveSpeed;

    // Actualizar la dirección de la mirada
    if (direction.sqrMagnitude > 0.01f)
    {
        if (Mathf.Abs(direction.x) > 0.1f)
            facing = new Vector2(Mathf.Sign(direction.x), 0f);
        else if (Mathf.Abs(direction.y) > 0.1f)
            facing = new Vector2(0f, Mathf.Sign(direction.y));
    }

    // Control del sonido de los pasos SOLO si se está moviendo físicamente en el mundo
    if (rb.linearVelocity.sqrMagnitude > 0.05f) // Nota: Si estás en una versión vieja de Unity anterior a Unity 6, cambia linearVelocity por velocity
    {
        stepTimer += Time.fixedDeltaTime;
        if (stepTimer >= stepInterval)
        {
            PlayFootstepSound();
            stepTimer = 0f;
        }
    }

    CheckIfStuck(position);
}*/

// Resolve horizontal travel before vertical travel to keep the sprite moving in one cardinal
// direction at a time.
private void FixedUpdate()
{
    if (!IsMoving)
    {
        Velocity = Vector2.zero;
        stepTimer = 0f;
        return;
    }

    Vector2 position = rb.position;
    Vector2 toTarget = currentTarget - position;

    // Allow at least one physics step of travel as arrival tolerance so the NPC can stop without
    // crossing back and forth.
    float threshold = Mathf.Max(
        arrivalThreshold,
        moveSpeed * Time.fixedDeltaTime
    );

    // Comprobar cada eje independientemente.
    bool reachedX = Mathf.Abs(toTarget.x) <= threshold;
    bool reachedY = Mathf.Abs(toTarget.y) <= threshold;

    // El NPC llegó cuando ambos ejes están dentro del umbral.
    if (reachedX && reachedY)
    {
        OnArrived();
        return;
    }

    // Movimiento en L: primero X, después Y.
    Vector2 direction = Vector2.zero;

    if (!reachedX)
    {
        direction = new Vector2(Mathf.Sign(toTarget.x), 0f);
    }
    else if (!reachedY)
    {
        direction = new Vector2(0f, Mathf.Sign(toTarget.y));
    }

    Velocity = direction * moveSpeed;

    // Actualizar la dirección de la mirada.
    if (direction.x != 0f)
    {
        facing = new Vector2(Mathf.Sign(direction.x), 0f);
    }
    else if (direction.y != 0f)
    {
        facing = new Vector2(0f, Mathf.Sign(direction.y));
    }

    // Reproducir pasos únicamente cuando exista movimiento físico.
    if (rb.linearVelocity.sqrMagnitude > 0.05f)
    {
        stepTimer += Time.fixedDeltaTime;

        if (stepTimer >= stepInterval)
        {
            PlayFootstepSound();
            stepTimer = 0f;
        }
    }

    CheckIfStuck(position);
}








    // ...and the Animator is updated last, once the state of this frame is settled.
    private void LateUpdate()
    {
        UpdateAnimator();
    }

    // ───────────────────── State machine ─────────────────────

    private void EnterIdle(float duration)
    {
        Velocity = Vector2.zero;
        state = State.Idle;
        stateTimer = duration;
    }

    private void EnterIdleRandom() =>
        EnterIdle(Random.Range(minPauseTime, Mathf.Max(minPauseTime, maxPauseTime)));

    /// <summary>Called when a pause ends: decides between returning home and continuing the routine.</summary>
  
    private void StartMovingTo(Vector2 target)
    {
        currentTarget = target;
        state = State.Moving;
        ResetStuckDetection();
    }

    private void StartReturningHome()
    {
        currentTarget = homePosition;
        state = State.ReturningHome;
        ResetStuckDetection();
    }

// After a home pause the NPC starts a fresh activity interval. A failed wander search schedules
// another attempt after a short idle.
private void DecideNextAction()
{
    // Si ya cumplió su tiempo de actividad fuera de casa, regresa
    if (returnToOrigin && activeTimer >= returnInterval && state != State.HomePause)
    {
        StartReturningHome();
        return;
    }

    // Si estaba haciendo pausa en casa, limpiamos el temporizador de actividad de inmediato
    if (state == State.HomePause)
    {
        activeTimer = 0f;
    }

    if (mode == MovementMode.Wander)
    {
        // Intentar obtener un punto válido para deambular
        if (TryGetWanderPoint(out Vector2 point))
        {
            StartMovingTo(point);
        }
        else
        {
            // Si no encontró un punto libre saliendo de casa, descansa un momento más y vuelve a intentar
            EnterIdle(1f); 
        }
    }
    else
    {
        StartMovingTo(GetNextPatrolPoint());
    }
}


/*private void OnArrived()
{
    Velocity = Vector2.zero;

    if (state == State.ReturningHome)
    {
        // Llegó al punto de origen: nos aseguramos de frenar en seco
        activeTimer = 0f;
        state = State.HomePause;
        stateTimer = homePauseTime;
        
        // Opcional: reposicionar ligeramente al centro exacto para evitar que se quede pegado al borde del collider
        rb.position = homePosition;
    }
    else
    {
        EnterIdleRandom();
    }
}
*/

// Returning home stops the body at its reached position and clears the stuck detector before the
// next departure.
private void OnArrived()
{
    Velocity = Vector2.zero;

    if (state == State.ReturningHome)
    {
        // Reiniciar el ciclo de actividad.
        activeTimer = 0f;

        // Entrar en pausa al llegar a casa.
        state = State.HomePause;
        stateTimer = homePauseTime;

        // Reiniciar la detección de atascos.
        ResetStuckDetection();
    }
    else
    {
        EnterIdleRandom();
    }
}





    // ───────────────────── Wander ─────────────────────

// Set obstacleMask to the blocking layers. Each wander attempt checks both the destination and
// the swept path using agentRadius.
private bool TryGetWanderPoint(out Vector2 point)
{
    Vector2 center = wanderFromOrigin ? homePosition : rb.position;
    Vector2 current = rb.position;

    for (int i = 0; i < maxPointAttempts; i++)
    {
        // Alternar equitativamente entre destinos horizontales y verticales
        bool horizontalMove = Random.value > 0.5f;
        float distance = Random.Range(minWanderDistance, wanderRadius);
        float sign = Random.value > 0.5f ? 1f : -1f;

        Vector2 candidate = horizontalMove 
            ? current + new Vector2(distance * sign, 0f) 
            : current + new Vector2(0f, distance * sign);

        Vector2 delta = candidate - current;

        if (obstacleMask.value != 0)
        {
            if (Physics2D.OverlapCircle(candidate, agentRadius, obstacleMask))
                continue;

            if (Physics2D.CircleCast(current, agentRadius, delta.normalized, delta.magnitude, obstacleMask))
                continue;
        }

        point = candidate;
        return true;
    }

    point = current; // Si falla, se mantiene seguro en su sitio en vez de crashear
    return false;
}


    // ───────────────────── Patrol ─────────────────────

    private Vector2 GetNextPatrolPoint()
    {
        Vector2 point = patrolPoints[patrolIndex].position;

        if (patrolPoints.Length > 1)
        {
            if (pingPong)
            {
                int next = patrolIndex + patrolDirection;
                if (next < 0 || next >= patrolPoints.Length)
                {
                    patrolDirection = -patrolDirection;
                    next = patrolIndex + patrolDirection;
                }
                patrolIndex = next;
            }
            else
            {
                patrolIndex = (patrolIndex + 1) % patrolPoints.Length;
            }
        }

        return point;
    }

    // ───────────────────── Stuck detection ─────────────────────

    private void ResetStuckDetection()
    {
        stuckCheckTimer = 0f;
        stuckAccumulated = 0f;
        lastCheckPosition = rb.position;
    }

   

/*
   private void CheckIfStuck(Vector2 position)
{
    stuckCheckTimer += Time.fixedDeltaTime;
    if (stuckCheckTimer < StuckCheckInterval)
        return;

    float moved = Vector2.Distance(position, lastCheckPosition);
    
    // Si se ha movido muy poco en este intervalo, acumulamos tiempo de atasco
    if (moved < 0.05f)
    {
        stuckAccumulated += stuckCheckTimer;
    }
    else
    {
        stuckAccumulated = 0f; // Se está moviendo bien, reiniciamos el contador
    }

    stuckCheckTimer = 0f;
    lastCheckPosition = position;

    if (stuckAccumulated >= stuckTimeout)
    {
        // Si está atascado contra un muro, forzamos una pausa y un nuevo cálculo limpio
        EnterIdleRandom();
    }
}
*/
// Measure actual displacement across physics samples. Sustained lack of progress cancels the
// destination and returns the NPC to idle.
private void CheckIfStuck(Vector2 position)
{
    stuckCheckTimer += Time.fixedDeltaTime;
    if (stuckCheckTimer < StuckCheckInterval)
        return;

    float moved = Vector2.Distance(position, lastCheckPosition);

    // Si el NPC está intentando moverse pero se ha movido menos de un píxel (chocando contra un Tilemap)
    if (moved < 0.02f)
    {
        stuckAccumulated += stuckCheckTimer;
    }
    else
    {
        stuckAccumulated = 0f; // Se está moviendo con libertad
    }

    stuckCheckTimer = 0f;
    lastCheckPosition = position;

    // Si pasa más de 0.6 segundos patinando contra una pared, cancelamos el destino actual
    if (stuckAccumulated >= 0.6f) 
    {
        stuckAccumulated = 0f;
        Velocity = Vector2.zero;
        EnterIdleRandom(); // Forzar pausa y buscar nuevo rumbo libre de obstáculos
    }
}






    // ───────────────────── Facing ─────────────────────

    private Vector2 GetInitialFacingVector()
    {
        switch (initialFacing)
        {
            case InitialFacing.Up:    return Vector2.up;
            case InitialFacing.Left:  return Vector2.left;
            case InitialFacing.Right: return Vector2.right;
            default:                  return Vector2.down;
        }
    }

    /// <summary>
    /// Converts a free direction into one of the 4 cardinal directions.
    /// Hysteresis keeps the current axis until the other one clearly dominates, preventing flicker on diagonals.
    /// </summary>
    private void UpdateFacing(Vector2 direction)
    {
        float absX = Mathf.Abs(direction.x);
        float absY = Mathf.Abs(direction.y);
        bool currentlyHorizontal = facing.x != 0f;

        bool useHorizontal = currentlyHorizontal
            ? !(absY > absX * directionHysteresis)   // stay horizontal unless vertical clearly wins
            : absX > absY * directionHysteresis;     // stay vertical unless horizontal clearly wins

        facing = useHorizontal
            ? new Vector2(Mathf.Sign(direction.x), 0f)
            : new Vector2(0f, Mathf.Sign(direction.y));
    }

    // ───────────────────── Animator ─────────────────────

    private void InitializeAnimator()
    {
        if (animator == null)
            animator = GetComponent<Animator>();

        if (animator == null || animator.runtimeAnimatorController == null)
        {
            Debug.LogWarning($"[{name}] No Animator or Animator Controller found. Animation updates disabled.", this);
            animatorReady = false;
            return;
        }

        hashMoveX = Animator.StringToHash(moveXParam);
        hashMoveY = Animator.StringToHash(moveYParam);
        hashIsWalking = Animator.StringToHash(isWalkingParam);
        hashFacingX = Animator.StringToHash(facingXParam);
        hashFacingY = Animator.StringToHash(facingYParam);

        // Cache which parameters really exist in the controller (done once, never per frame).
        foreach (AnimatorControllerParameter p in animator.parameters)
        {
            if (p.type == AnimatorControllerParameterType.Float)
            {
                if (p.nameHash == hashMoveX) hasMoveX = true;
                else if (p.nameHash == hashMoveY) hasMoveY = true;
                else if (p.nameHash == hashFacingX) hasFacingX = true;
                else if (p.nameHash == hashFacingY) hasFacingY = true;
            }
            else if (p.type == AnimatorControllerParameterType.Bool && p.nameHash == hashIsWalking)
            {
                hasIsWalking = true;
            }
        }

        if (!hasMoveX || !hasMoveY)
            Debug.LogWarning($"[{name}] Animator is missing Float parameters '{moveXParam}' and/or '{moveYParam}'.", this);
        if (!hasIsWalking)
            Debug.LogWarning($"[{name}] Animator is missing Bool parameter '{isWalkingParam}'.", this);

        animatorReady = true;
    }

// Keep the last facing direction when idle and write Animator parameters only when their values
// change.
private void UpdateAnimator()
{
    if (!animatorReady)
        return;

    bool walking = IsMoving;

    // Tomar la dirección actual de la mirada (facing)
    float moveX = facing.x;
    float moveY = facing.y;

    // Enviar los valores a los parámetros del Animator (Horizontal y Vertical)
    if (hasMoveX && !Mathf.Approximately(moveX, sentMoveX))
    {
        animator.SetFloat(hashMoveX, moveX);
        sentMoveX = moveX;
    }

    if (hasMoveY && !Mathf.Approximately(moveY, sentMoveY))
    {
        animator.SetFloat(hashMoveY, moveY);
        sentMoveY = moveY;
    }

    // Activar o desactivar el booleano IsWalking para cambiar entre Idle y Run
    int walkingValue = walking ? 1 : 0;
    if (hasIsWalking && walkingValue != sentWalking)
    {
        animator.SetBool(hashIsWalking, walking);
        sentWalking = walkingValue;
    }
}

    // ───────────────────── Editor gizmos ─────────────────────

    private void OnDrawGizmosSelected()
    {
        Vector3 home = Application.isPlaying ? (Vector3)homePosition : transform.position;

        // Wander radius
        if (mode == MovementMode.Wander)
        {
            Gizmos.color = new Color(0f, 1f, 0.3f, 0.8f);
            Vector3 center = (wanderFromOrigin || !Application.isPlaying) ? home : transform.position;
            Gizmos.DrawWireSphere(center, wanderRadius);
        }

        // Spawn point
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(home, Vector3.one * 0.3f);

        // Patrol route
        if (mode == MovementMode.Patrol && patrolPoints != null && patrolPoints.Length > 0)
        {
            Gizmos.color = Color.yellow;
            for (int i = 0; i < patrolPoints.Length; i++)
            {
                if (patrolPoints[i] == null) continue;
                Gizmos.DrawSphere(patrolPoints[i].position, 0.15f);

                int next = (i + 1) % patrolPoints.Length;
                if ((pingPong && i == patrolPoints.Length - 1) || patrolPoints[next] == null) continue;
                Gizmos.DrawLine(patrolPoints[i].position, patrolPoints[next].position);
            }
        }

        // Current target
        if (Application.isPlaying && IsMoving)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, currentTarget);
            Gizmos.DrawWireSphere(currentTarget, 0.15f);
        }
    }

//////Auxiliar////////
private void PlayFootstepSound()
{
    if (audioSource == null || footstepClips == null || footstepClips.Length == 0)
        return;

    // Elige un sonido aleatorio de la lista para que no suene robótico
    AudioClip clip = footstepClips[Random.Range(0, footstepClips.Length)];
    audioSource.PlayOneShot(clip);
}




}

